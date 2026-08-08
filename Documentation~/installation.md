# Installation

## Development mode

During active framework development, place the HP Framework repository directly inside the Unity project's Assets tree:

```text
Assets/Plugins/HPFramework/
```

This is the preferred development setup because all runtime and editor code is immediately editable, debuggable and visible in the IDE. The `.git` directory may remain inside `Assets/Plugins/HPFramework`; Unity ignores it while Git continues to treat the framework as its own repository.

Setup creates host-project Bootstrap/settings alongside the editable framework source:

```text
Assets/Plugins/HPFramework/
├── Generated/
└── Settings/
```

Those two folders are intentionally Git-ignored because their contents are generated or project-specific. The reusable Bootstrap template remains versioned at `Editor/Templates/Bootstrap.prefab`.

HP Framework targets Unity 6. VContainer, UniTask and the private Json.NET build are bundled under `ThirdParty`.

UGUI is a required project dependency. The Input System integration is optional while the framework is consumed directly from `Assets/Plugins`: `HP.Framework.Input` uses a package version define and is excluded when `com.unity.inputsystem` is absent, while Bootstrap/Editor avoid a hard compile-time reference to that module. Future UPM installs still declare Input System in `package.json`.

After placing the framework under `Assets/Plugins`, run `Tools > HP Framework > Setup`. Use **Setup / Repair Missing References** for normal repair; it preserves existing project UI configuration. Use **Reset Bootstrap To Framework Defaults** only for an intentional reset.

## Future package distribution

During active development, HP Framework is intended to live as editable source under `Assets/Plugins/HPFramework`. The repository still keeps `package.json`, `Runtime`, `Editor`, `Tests`, `Samples~` and `Documentation~` in a UPM-compatible layout so the same codebase can later be distributed through Git/local UPM without another architecture migration.

`package.json` is retained for future UPM distribution, but while the framework is under `Assets/Plugins`, Unity Package Manager does not automatically install the dependencies declared there. Input System integration is optional at compile time; the rest of the framework can compile without `com.unity.inputsystem`. UGUI remains a required Unity UI dependency.
