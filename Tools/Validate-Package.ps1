param(
    [string]$PackageRoot = "_Base"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Split-Path -Parent $PSScriptRoot)).Path
$root = Join-Path $repoRoot $PackageRoot
$repoPrefix = $repoRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar

if (-not (Test-Path $root -PathType Container)) {
    Write-Error "Package root not found: $root"
    exit 2
}

$errors = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()

function Add-Error([string]$message) { $script:errors.Add($message) }
function Add-Warning([string]$message) { $script:warnings.Add($message) }
function Relative([string]$path) {
    $fullPath = [IO.Path]::GetFullPath($path)
    if ($fullPath.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($repoPrefix.Length)
    }
    return $fullPath
}

# Every Unity asset and folder must have a .meta file.
Get-ChildItem $root -Recurse -File |
    Where-Object { $_.Extension -ne ".meta" -and -not (Test-Path ($_.FullName + ".meta")) } |
    ForEach-Object { Add-Error "Missing meta: $(Relative $_.FullName)" }

Get-ChildItem $root -Recurse -Directory |
    Where-Object { -not (Test-Path ($_.FullName + ".meta")) } |
    ForEach-Object { Add-Error "Missing folder meta: $(Relative $_.FullName)" }

Get-ChildItem $root -Recurse -Filter *.meta -File | ForEach-Object {
    $assetPath = $_.FullName.Substring(0, $_.FullName.Length - 5)
    if (-not (Test-Path $assetPath)) {
        Add-Error "Orphan meta: $(Relative $_.FullName)"
    }
}

# Validate and index GUIDs.
$guidOwners = @{}
Get-ChildItem $root -Recurse -Filter *.meta -File | ForEach-Object {
    $match = Select-String -Path $_.FullName -Pattern '^guid:\s*([0-9a-f]{32})$' | Select-Object -First 1
    if (-not $match) {
        Add-Error "Invalid or missing GUID: $(Relative $_.FullName)"
        return
    }

    $guid = $match.Matches[0].Groups[1].Value
    if ($guidOwners.ContainsKey($guid)) {
        Add-Error "Duplicate GUID ${guid}: $(Relative $guidOwners[$guid]) and $(Relative $_.FullName)"
    }
    else {
        $guidOwners[$guid] = $_.FullName
    }
}

# Parse package and all assembly definitions.
try {
    Get-Content (Join-Path $root "package.json") -Raw | ConvertFrom-Json | Out-Null
}
catch {
    Add-Error "Invalid package.json: $($_.Exception.Message)"
}

$assemblyNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$assemblyDefinitions = @()
Get-ChildItem $root -Recurse -Filter *.asmdef -File | ForEach-Object {
    try {
        $definition = Get-Content $_.FullName -Raw | ConvertFrom-Json
        if ([string]::IsNullOrWhiteSpace($definition.name)) {
            Add-Error "Assembly without name: $(Relative $_.FullName)"
        }
        elseif (-not $assemblyNames.Add([string]$definition.name)) {
            Add-Error "Duplicate assembly name '$($definition.name)': $(Relative $_.FullName)"
        }
        $assemblyDefinitions += [pscustomobject]@{ Path = $_.FullName; Definition = $definition }
    }
    catch {
        Add-Error "Invalid asmdef $(Relative $_.FullName): $($_.Exception.Message)"
    }
}

$allowedExternalAssemblies = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@(
        "Unity.InputSystem",
        "Unity.Newtonsoft.Json",
        "Unity.TextMeshPro",
        "Unity.UGUI"
    ),
    [StringComparer]::Ordinal)

foreach ($entry in $assemblyDefinitions) {
    foreach ($reference in @($entry.Definition.references)) {
        $referenceText = [string]$reference
        if ($referenceText.StartsWith("GUID:", [StringComparison]::Ordinal)) {
            $guid = $referenceText.Substring(5)
            if (-not $guidOwners.ContainsKey($guid)) {
                Add-Error "Unresolved asmdef GUID '$guid' in $(Relative $entry.Path)"
            }
            continue
        }

        if (-not $assemblyNames.Contains($referenceText) -and
            -not $allowedExternalAssemblies.Contains($referenceText)) {
            Add-Error "Unresolved assembly reference '$referenceText' in $(Relative $entry.Path)"
        }
    }
}

# Validate serialized MonoBehaviour script GUIDs.
$externalScriptGuids = [System.Collections.Generic.HashSet[string]]::new(
    [StringComparer]::Ordinal)
[void]$externalScriptGuids.Add("0cd44c1031e13a943bb63640046fad76") # CanvasScaler
[void]$externalScriptGuids.Add("dc42784cf147c0c48a680349fa168899") # GraphicRaycaster
[void]$externalScriptGuids.Add("fe87c0e1cc204ed48ad3b37840f39efc") # Image
[void]$externalScriptGuids.Add("5f7201a12d95ffc409449d95f23cf332") # Text
[void]$externalScriptGuids.Add("4e29b1a8efbd4b44bb3f3716e73f07ff") # Button

Get-ChildItem $root -Recurse -Include *.prefab,*.asset -File |
    Select-String -Pattern 'm_Script:\s*\{fileID:\s*11500000, guid:\s*([^,}]+)' |
    ForEach-Object {
        $guid = $_.Matches[0].Groups[1].Value.Trim()
        $location = "$(Relative $_.Path):$($_.LineNumber)"
        if ($guid -notmatch '^[0-9a-f]{32}$') {
            Add-Error "Invalid script GUID '$guid' at $location"
        }
        elseif (-not $guidOwners.ContainsKey($guid) -and -not $externalScriptGuids.Contains($guid)) {
            Add-Error "Unresolved script GUID '$guid' at $location"
        }
    }

# Prevent known architecture regressions in framework-owned code.
$sourceRoots = @(
    (Join-Path $root "Scripts"),
    (Join-Path $root "Graphics"),
    (Join-Path $root "Diagnostics")
)
$forbiddenPatterns = @(
    @{ Pattern = 'guid:\s*(RootLifetimeScope|AudioManager|UIManager|InputManager|GameSceneManager|PoolManager|HapticManager)'; Message = 'symbolic prefab GUID' },
    @{ Pattern = '\bMain\.Instance\b'; Message = 'legacy Main service locator' },
    @{ Pattern = '\bMonoManager\b'; Message = 'legacy MonoManager lifecycle' },
    @{ Pattern = 'using\s+DG\.Tweening\s*;'; Message = 'hard DOTween dependency' },
    @{ Pattern = 'using\s+Sirenix\.OdinInspector\s*;'; Message = 'hard Odin dependency' },
    @{ Pattern = 'Resources\.UnloadUnusedAssets\s*\('; Message = 'unconditional global unload' },
    @{ Pattern = '`n'; Message = 'literal PowerShell newline token' }
)

foreach ($sourceRoot in $sourceRoots) {
    if (-not (Test-Path $sourceRoot)) { continue }
    Get-ChildItem $sourceRoot -Recurse -Filter *.cs -File | ForEach-Object {
        $content = Get-Content $_.FullName -Raw
        foreach ($rule in $forbiddenPatterns) {
            if ($content -match $rule.Pattern) {
                Add-Error "$($rule.Message): $(Relative $_.FullName)"
            }
        }
    }
}

if ($warnings.Count -gt 0) {
    Write-Host "Warnings:" -ForegroundColor Yellow
    $warnings | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
}

if ($errors.Count -gt 0) {
    Write-Host "Package validation failed with $($errors.Count) error(s):" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "Package validation passed." -ForegroundColor Green
Write-Host "  Assemblies: $($assemblyDefinitions.Count)"
Write-Host "  GUIDs: $($guidOwners.Count)"
Write-Host "  Missing meta: 0"
Write-Host "  Invalid script GUID: 0"
exit 0
