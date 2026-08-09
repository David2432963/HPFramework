# Installation

## Development mode

During active framework development, place the HP Framework repository directly inside the Unity project's Assets tree:

```text
Assets/Plugins/HPFramework/
```

This is the preferred workflow while the framework is still evolving because runtime/editor source remains immediately editable, debuggable and visible in the consuming project. The framework may keep its own nested `.git` directory; Unity ignores it while Git continues to treat `Assets/Plugins/HPFramework` as an independent repository.

The expected source layout is:

```text
Assets/Plugins/HPFramework/
├── Runtime/
├── Editor/
├── Tests/
├── ThirdParty/
├── Samples~/
├── Documentation~/
└── package.json
```

Setup creates host-project outputs beside the editable source:

```text
Assets/Plugins/HPFramework/
├── Generated/
│   └── Bootstrap.prefab
└── Settings/
    ├── VContainerSettings.asset
    ├── DefaultAudioLibrary.asset
    └── DefaultUICatalog.asset
```

`Generated/` and `Settings/` are intentionally excluded from the framework Git repository because they are generated/project-specific. The reusable Bootstrap source stays versioned at `Editor/Templates/Bootstrap.prefab`.

## Requirements

HP Framework targets Unity 6 (`6000.0` or newer).

Current dependency rules:

- UGUI is required by the current UI/Graphics/Diagnostics stack.
- VContainer and UniTask are bundled under `ThirdParty/`.
- Json.NET 13.0.4 is bundled as the private `HP.Framework.NewtonsoftJson` assembly for internal framework extension use.
- Input System is optional while the framework is consumed directly from `Assets/Plugins`. `HP.Framework.Input` uses a package version define and is excluded when `com.unity.inputsystem` is absent.
- Editor/Bootstrap setup avoids a hard compile-time dependency on the optional Input assembly.

`package.json` still declares Input System and UGUI for future UPM consumption. When the framework is placed under `Assets/Plugins`, Package Manager metadata does not automatically install those dependencies.

## First project setup

After placing the framework under `Assets/Plugins`, open:

```text
Tools > HP Framework > Setup
```

For normal setup or repair, use **Setup / Repair Missing References**. Repair is intentionally non-destructive: it fills missing references/objects while preserving existing manager ownership, camera settings, Canvas Scaler values, layout, catalogs, Input Actions and Toast assignments.

Use **Reset Bootstrap To Framework Defaults** only when you intentionally want HP Framework to restore its canonical domain-owned Bootstrap hierarchy and stock defaults.

After setup, run **Validate Project**.

## Future package distribution

The repository keeps a UPM-compatible root (`package.json`, `Runtime`, `Editor`, `Tests`, `Samples~`, `Documentation~`) so the same codebase can later be consumed through Git/local UPM without another structural migration.

Editable `Assets/Plugins/HPFramework` consumption remains the intended workflow during active development.

Before publishing or redistributing the framework publicly, choose explicit HP Framework license terms and verify the retained Safe Area helper's redistribution terms. See `THIRD PARTY NOTICES.md` for bundled dependency notices.
