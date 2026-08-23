# Installation

## Development mode

During active framework development, place the HP Framework repository directly inside the Unity project's Assets tree:

```text
Assets/Plugins/HPFramework/
```

This is the preferred workflow while the framework is still evolving because runtime/editor source remains immediately editable, debuggable and visible in the consuming project. The framework may keep its own nested `.git` directory; Unity ignores it while Git continues to treat `Assets/Plugins/HPFramework` as an independent repository.

The expected source layout includes the canonical runtime assets:

```text
Assets/Plugins/HPFramework/
├── Runtime/
│   ├── Bootstrap/Prefabs/Bootstrap.prefab
│   ├── Bootstrap/Loading/Scenes/LoadingScene.unity
│   └── Defaults/
│       ├── VContainerSettings.asset
│       ├── DefaultAudioLibrary.asset
│       ├── DefaultUICatalog.asset
│       └── Performance/
├── Editor/
├── Tests/
├── ThirdParty/
├── Samples~/
├── Documentation~/
└── package.json
```

These assets are versioned with stable Unity GUIDs. HP Framework no longer generates a per-machine Bootstrap or default-settings copy.

## Requirements

HP Framework targets Unity 6 (`6000.0` or newer).

Current dependency rules:

- UGUI is required by the current UI/Graphics/Diagnostics stack.
- VContainer and UniTask are bundled under `ThirdParty/`.
- Json.NET 13.0.4 is bundled as the private `HP.Framework.NewtonsoftJson` assembly for internal framework extension use.
- Input System and UGUI are required by the zero-setup canonical Bootstrap and are declared in `package.json`.

When the repository is consumed directly under `Assets/Plugins`, the consuming Unity project must have those declared packages available.

## First project use

After placing the framework under `Assets/Plugins`, drag `Runtime/Bootstrap/Prefabs/Bootstrap.prefab` into the application entry scene. The prefab is already wired to valid framework-owned Audio/UI/Input/Performance defaults.

Project-specific configuration belongs on that prefab instance. For example, a game may override the instance's `UICatalog` or `InputActionAsset`; those overrides are serialized by the scene and must not be applied back to the framework prefab.

`Tools > HP Framework > Setup` is optional. Use it for validation, repair, diagnostics or explicit project integration. Normal Repair fills only missing references and preserves valid project overrides.

## Future package distribution

The repository keeps a UPM-compatible root (`package.json`, `Runtime`, `Editor`, `Tests`, `Samples~`, `Documentation~`) so the same codebase can later be consumed through Git/local UPM without another structural migration.

Editable `Assets/Plugins/HPFramework` consumption remains the intended workflow during active development.

Before publishing or redistributing the framework publicly, choose explicit HP Framework license terms and verify the retained Safe Area helper's redistribution terms. See `THIRD PARTY NOTICES.md` for bundled dependency notices.
