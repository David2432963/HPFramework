param(
    [string]$PackageRoot
)

$ErrorActionPreference = "Stop"
$repoRoot = if ([string]::IsNullOrWhiteSpace($PackageRoot)) {
    (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
} else {
    (Resolve-Path $PackageRoot).Path
}
$repoPrefix = $repoRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$errors = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()

function Add-Error([string]$message) { $script:errors.Add($message) }
function Add-Warning([string]$message) { $script:warnings.Add($message) }
function Relative([string]$path) {
    $full = [IO.Path]::GetFullPath($path)
    if ($full.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($repoPrefix.Length).Replace('\','/')
    }
    return $full
}

$packageJsonPath = Join-Path $repoRoot "package.json"
if (-not (Test-Path $packageJsonPath -PathType Leaf)) {
    Add-Error "Missing package.json at repository root."
    $package = $null
} else {
    try { $package = Get-Content $packageJsonPath -Raw | ConvertFrom-Json }
    catch { Add-Error "Invalid package.json: $($_.Exception.Message)"; $package = $null }
}

if ($package) {
    if ($package.name -ne "com.hp.framework") { Add-Error "package.json name must be 'com.hp.framework'." }
    if ($package.displayName -ne "HP Framework") { Add-Error "package.json displayName must be 'HP Framework'." }
    if ([string]$package.version -notmatch '^\d+\.\d+\.\d+$') { Add-Error "package.json version must use SemVer x.y.z." }
    if ([string]$package.unity -notmatch '^6000\.') { Add-Warning "HP Framework is currently validated against Unity 6; package.json unity is '$($package.unity)'." }
    if ($package.dependencies.'com.unity.nuget.newtonsoft-json') { Add-Error "Newtonsoft is bundled under ThirdParty/NewtonsoftJson and must not be declared as an external package dependency." }
    $bundledNewtonsoft = Join-Path $repoRoot "ThirdParty\NewtonsoftJson\HP.Framework.NewtonsoftJson.dll"
    if (-not (Test-Path $bundledNewtonsoft -PathType Leaf)) {
        Add-Error "Missing bundled Newtonsoft assembly: ThirdParty/NewtonsoftJson/HP.Framework.NewtonsoftJson.dll"
    } else {
        try {
            $identity = [Reflection.AssemblyName]::GetAssemblyName($bundledNewtonsoft)
            if ($identity.Name -ne 'HP.Framework.NewtonsoftJson') { Add-Error "Bundled Newtonsoft assembly name must be HP.Framework.NewtonsoftJson, got '$($identity.Name)'." }
            if ($identity.Version.ToString() -ne '13.0.0.0') { Add-Error "Bundled Newtonsoft assembly version must be 13.0.0.0, got '$($identity.Version)'." }
        } catch { Add-Error "Unable to inspect bundled Newtonsoft assembly: $($_.Exception.Message)" }

        $bundledNewtonsoftMeta = $bundledNewtonsoft + '.meta'
        if (-not (Test-Path $bundledNewtonsoftMeta -PathType Leaf)) {
            Add-Error "Missing bundled Newtonsoft dll meta."
        } elseif ((Get-Content $bundledNewtonsoftMeta -Raw) -notmatch 'isExplicitlyReferenced:\s*1') {
            Add-Error "Bundled Newtonsoft must have Auto Reference disabled (isExplicitlyReferenced: 1)."
        }
    }

    $extensionsAsmdefPath = Join-Path $repoRoot "Runtime\Extensions\HP.Framework.Extensions.asmdef"
    if (Test-Path $extensionsAsmdefPath -PathType Leaf) {
        try {
            $extensionsAsmdef = Get-Content $extensionsAsmdefPath -Raw | ConvertFrom-Json
            if (@($extensionsAsmdef.precompiledReferences) -notcontains 'HP.Framework.NewtonsoftJson.dll') {
                Add-Error "HP.Framework.Extensions must explicitly reference HP.Framework.NewtonsoftJson.dll."
            }
            if (@($extensionsAsmdef.precompiledReferences) -contains 'Newtonsoft.Json.dll') {
                Add-Error "HP.Framework.Extensions must not reference external Newtonsoft.Json.dll."
            }
        } catch { Add-Error "Invalid HP.Framework.Extensions asmdef: $($_.Exception.Message)" }
    }
}

$requiredRoots = @("Runtime", "Editor", "Tests", "ThirdParty")
foreach ($name in $requiredRoots) {
    if (-not (Test-Path (Join-Path $repoRoot $name) -PathType Container)) {
        Add-Error "Missing required package folder: $name"
    }
}

$forbiddenTopLevel = @("Assets", "Packages", "ProjectSettings", "Library", "Temp", "Logs", "UserSettings", "Obj", "Build", "Builds", "DevProject", "_Base")
foreach ($name in $forbiddenTopLevel) {
    if (Test-Path (Join-Path $repoRoot $name)) { Add-Error "Forbidden publish folder at package root: $name" }
}

$forbiddenAnywhere = @("Sirenix", "WaypointPathfinding")
foreach ($name in $forbiddenAnywhere) {
    $hit = Get-ChildItem $repoRoot -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq $name } | Select-Object -First 1
    if ($hit) { Add-Error "Forbidden legacy content remains: $(Relative $hit.FullName)" }
}

$unityRoots = @()
foreach ($name in $requiredRoots) {
    $path = Join-Path $repoRoot $name
    if (Test-Path $path) { $unityRoots += $path }
}

# Unity-imported package content must retain metas. Hidden UPM folders (Samples~/Documentation~/.github) are intentionally excluded.
foreach ($root in $unityRoots) {
    Get-ChildItem $root -Recurse -File -Force |
        Where-Object { $_.Extension -ne '.meta' -and -not (Test-Path ($_.FullName + '.meta')) } |
        ForEach-Object { Add-Error "Missing meta: $(Relative $_.FullName)" }

    Get-ChildItem $root -Recurse -Directory -Force |
        Where-Object { -not (Test-Path ($_.FullName + '.meta')) } |
        ForEach-Object { Add-Error "Missing folder meta: $(Relative $_.FullName)" }

    Get-ChildItem $root -Recurse -Filter *.meta -File -Force | ForEach-Object {
        $assetPath = $_.FullName.Substring(0, $_.FullName.Length - 5)
        if (-not (Test-Path $assetPath)) { Add-Error "Orphan meta: $(Relative $_.FullName)" }
    }
}

$guidOwners = @{}
foreach ($root in $unityRoots) {
    Get-ChildItem $root -Recurse -Filter *.meta -File -Force | ForEach-Object {
        $match = Select-String -Path $_.FullName -Pattern '^guid:\s*([0-9a-f]{32})$' | Select-Object -First 1
        if (-not $match) { Add-Error "Invalid or missing GUID: $(Relative $_.FullName)"; return }
        $guid = $match.Matches[0].Groups[1].Value
        if ($guidOwners.ContainsKey($guid)) {
            Add-Error "Duplicate GUID ${guid}: $(Relative $guidOwners[$guid]) and $(Relative $_.FullName)"
        } else { $guidOwners[$guid] = $_.FullName }
    }
}

$assemblyNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$assemblyDefinitions = @()
foreach ($root in $unityRoots) {
    Get-ChildItem $root -Recurse -Filter *.asmdef -File | ForEach-Object {
        try {
            $definition = Get-Content $_.FullName -Raw | ConvertFrom-Json
            if ([string]::IsNullOrWhiteSpace($definition.name)) { Add-Error "Assembly without name: $(Relative $_.FullName)" }
            elseif (-not $assemblyNames.Add([string]$definition.name)) { Add-Error "Duplicate assembly name '$($definition.name)': $(Relative $_.FullName)" }
            $assemblyDefinitions += [pscustomobject]@{ Path = $_.FullName; Definition = $definition }
        } catch { Add-Error "Invalid asmdef $(Relative $_.FullName): $($_.Exception.Message)" }
    }
}

$requiredAssemblies = @(
    "HP.Framework.Core", "HP.Framework.Assets", "HP.Framework.Audio", "HP.Framework.Bootstrap",
    "HP.Framework.Editor", "HP.Framework.Haptics", "HP.Framework.Input", "HP.Framework.Persistence",
    "HP.Framework.Pooling", "HP.Framework.UI", "HP.Framework.UI.TMP",
    "HP.Framework.Graphics", "HP.Framework.Diagnostics", "HP.Framework.Extensions", "HP.Framework.SafeArea",
    "HP.Framework.Tests.Editor", "HP.Framework.Tests.Runtime", "VContainer", "UniTask"
)
foreach ($name in $requiredAssemblies) {
    if (-not $assemblyNames.Contains($name)) { Add-Error "Required assembly missing: $name" }
}

$core = $assemblyDefinitions | Where-Object { $_.Definition.name -eq 'HP.Framework.Core' } | Select-Object -First 1
if ($core -and @($core.Definition.references).Count -ne 0) {
    Add-Error "HP.Framework.Core must not reference other assemblies."
}

$allowedExternalAssemblies = [System.Collections.Generic.HashSet[string]]::new([string[]]@(
    "Unity.InputSystem", "Unity.TextMeshPro", "Unity.UGUI"
), [StringComparer]::Ordinal)

foreach ($entry in $assemblyDefinitions) {
    foreach ($reference in @($entry.Definition.references)) {
        $ref = [string]$reference
        if ($ref.StartsWith('GUID:', [StringComparison]::Ordinal)) {
            $guid = $ref.Substring(5)
            if (-not $guidOwners.ContainsKey($guid)) { Add-Error "Unresolved asmdef GUID '$guid' in $(Relative $entry.Path)" }
        } elseif (-not $assemblyNames.Contains($ref) -and -not $allowedExternalAssemblies.Contains($ref)) {
            Add-Error "Unresolved assembly reference '$ref' in $(Relative $entry.Path)"
        }
    }
}

# Internal named-assembly cycle detection.
$internalNames = @($assemblyDefinitions | ForEach-Object { [string]$_.Definition.name })
$indegree = @{}; $edges = @{}
foreach ($name in $internalNames) { $indegree[$name] = 0; $edges[$name] = [System.Collections.Generic.List[string]]::new() }
foreach ($entry in $assemblyDefinitions) {
    $from = [string]$entry.Definition.name
    foreach ($ref in @($entry.Definition.references)) {
        $to = [string]$ref
        if ($indegree.ContainsKey($to)) { $edges[$to].Add($from); $indegree[$from]++ }
    }
}
$queue = [System.Collections.Generic.Queue[string]]::new()
foreach ($name in $internalNames) { if ($indegree[$name] -eq 0) { $queue.Enqueue($name) } }
$visited = 0
while ($queue.Count -gt 0) {
    $name = $queue.Dequeue(); $visited++
    foreach ($next in $edges[$name]) { $indegree[$next]--; if ($indegree[$next] -eq 0) { $queue.Enqueue($next) } }
}
if ($visited -ne $internalNames.Count) { Add-Error "Internal asmdef dependency cycle detected." }

$externalScriptGuids = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
@(
    "0cd44c1031e13a943bb63640046fad76", # CanvasScaler
    "dc42784cf147c0c48a680349fa168899", # GraphicRaycaster
    "fe87c0e1cc204ed48ad3b37840f39efc", # Image
    "76c392e42b5098c458856cdf6ecaaaa1", # EventSystem
    "5f7201a12d95ffc409449d95f23cf332", # Text
    "4e29b1a8efbd4b44bb3f3716e73f07ff"  # Button
) | ForEach-Object { [void]$externalScriptGuids.Add($_) }

foreach ($root in $unityRoots) {
    Get-ChildItem $root -Recurse -Include *.prefab,*.asset -File |
        Select-String -Pattern 'm_Script:\s*\{fileID:\s*11500000, guid:\s*([^,}]+)' |
        ForEach-Object {
            $guid = $_.Matches[0].Groups[1].Value.Trim()
            $location = "$(Relative $_.Path):$($_.LineNumber)"
            if ($guid -notmatch '^[0-9a-f]{32}$') { Add-Error "Invalid script GUID '$guid' at $location" }
            elseif (-not $guidOwners.ContainsKey($guid) -and -not $externalScriptGuids.Contains($guid)) { Add-Error "Unresolved script GUID '$guid' at $location" }
        }
}

$sourceRoots = @((Join-Path $repoRoot 'Runtime'), (Join-Path $repoRoot 'Editor'))
$forbiddenPatterns = @(
    @{ Pattern = '\bMain\.Instance\b'; Message = 'legacy Main service locator' },
    @{ Pattern = '\bMonoManager\b'; Message = 'legacy MonoManager lifecycle' },
    @{ Pattern = 'using\s+DG\.Tweening\s*;'; Message = 'hard DOTween dependency' },
    @{ Pattern = 'using\s+Sirenix\.'; Message = 'hard Odin/Sirenix dependency' },
    @{ Pattern = 'Resources\.UnloadUnusedAssets\s*\('; Message = 'unconditional global unload' },
    @{ Pattern = 'DontDestroyOnLoad\s*\('; Message = 'hidden global lifetime' },
    @{ Pattern = 'LifetimeScope\.Find\s*<'; Message = 'runtime LifetimeScope service locator' },
    @{ Pattern = '\.Container\.Resolve\s*<'; Message = 'runtime container service locator' },
    @{ Pattern = 'public\s+static\s+[^\r\n;=]+\s+Instance\s*[{=]'; Message = 'public static singleton Instance' },
    @{ Pattern = 'Assets/_Base|Assets/BaseData|Assets/BaseSettings|com\.base\.vcontainer|KeyboardEscape|NewFolderGames'; Message = 'legacy/project-specific identifier' },
    @{ Pattern = '`r`n|`n'; Message = 'literal PowerShell newline token' }
)
foreach ($root in $sourceRoots) {
    if (-not (Test-Path $root)) { continue }
    Get-ChildItem $root -Recurse -Filter *.cs -File | ForEach-Object {
        $content = Get-Content $_.FullName -Raw
        foreach ($rule in $forbiddenPatterns) {
            if ($content -match $rule.Pattern) { Add-Error "$($rule.Message): $(Relative $_.FullName)" }
        }
    }
}

if ($warnings.Count -gt 0) {
    Write-Host "Warnings:" -ForegroundColor Yellow
    $warnings | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
}
if ($errors.Count -gt 0) {
    Write-Host "HP Framework validation failed with $($errors.Count) error(s):" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "HP Framework validation passed." -ForegroundColor Green
Write-Host "  Assemblies: $($assemblyDefinitions.Count)"
Write-Host "  GUIDs: $($guidOwners.Count)"
Write-Host "  Package: $($package.name) $($package.version)"
exit 0
