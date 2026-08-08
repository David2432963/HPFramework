# HP Framework

HP Framework is a VContainer-first Unity 6 game foundation. During active development it is intended to live directly under `Assets/Plugins/HPFramework` so all framework source remains editable inside the consuming Unity project. The repository keeps a UPM-compatible layout so it can be distributed as a package later without another structural rewrite.

## Requirements

- Unity 6 (`6000.0` or newer)
- UGUI is required by the UI/Graphics/Diagnostics modules
- Input System is an optional runtime module in editable `Assets/Plugins` development mode; the Input assembly is excluded cleanly when `com.unity.inputsystem` is absent
- Future UPM installs declare Input System and UGUI through `package.json`
- VContainer, UniTask and a private Json.NET 13.0.4 build are bundled under `ThirdParty/`

## Development install

While HP Framework is still being developed, clone or copy the repository directly to:

```text
Assets/Plugins/HPFramework/
```

This makes every runtime/editor source file editable from the project while preserving the framework's asmdef boundaries and independent Git repository.

During development, the tracked framework repository contains source, editor tooling, tests, samples, documentation and bundled third-party dependencies. `Generated/` and `Settings/` are host-project outputs created by Setup and are intentionally ignored by Git.

```text
Assets/Plugins/HPFramework/
├── Runtime/
├── Editor/
├── Tests/
├── Samples~/
├── Documentation~/
└── ThirdParty/
```

The repository remains UPM-compatible through `package.json`; Git/Package Manager distribution can be enabled later when the framework API is stable.

## First setup

After installation open:

```text
Tools > HP Framework > Setup
```

Choose **Setup / Repair Missing References**. HP Framework creates missing objects/references without resetting existing camera, Canvas Scaler, layout, catalog, Input Actions or Toast customizations. Use **Reset Bootstrap To Framework Defaults** only when you explicitly want the framework defaults reapplied.

HP Framework creates or repairs its editable framework assets directly under:

```text
Assets/Plugins/HPFramework/
├── Generated/Bootstrap.prefab
└── Settings/
    ├── VContainerSettings.asset
    ├── DefaultAudioLibrary.asset
    └── DefaultUICatalog.asset
```

These assets are local to the consuming Unity project and are intentionally excluded from the framework Git repository. The canonical Bootstrap source is `Editor/Templates/Bootstrap.prefab`; Setup generates/repairs the local `Generated/Bootstrap.prefab` and settings assets from framework tooling.

## Architecture

```text
RootLifetimeScope
├── BaseSceneLifetimeScope
│   └── BaseFeatureLifetimeScope
└── application-lifetime services
```

Use `RootLifetimeScope` for application lifetime only. Put gameplay state in scene/feature scopes. Scene and feature scopes can shadow `IUIService`, `IPoolService` and `IEventBus` with `RegisterScopedUI()`, `RegisterScopedPool()` and `RegisterScopeEventBus()` so dependencies follow the owning scope. New code should prefer the scoped `IEventBus`; the static `Observer` remains as a compatibility/global convenience API.

`RootLifetimeScope` inherits VContainer's early execution order (`-5000`) and builds synchronous framework services during `Awake()` when `autoRun` is enabled. For future async startup dependencies, use an explicit readiness/lifecycle barrier instead of relying only on `Awake()` order.

## UI runtime notes

When URP is installed, HP Framework configures `Camera.main` as a Base camera and the framework `UICamera` as Overlay, then attaches the UI camera to the main camera stack idempotently on initialization/scene load. The gameplay camera must use the `MainCamera` tag.

`BasePopup` includes edit-mode animation preview in its Inspector: Show, Hide, Show → Hide, Stop, Reset, scrub progress, speed, loop, and auto-preview on preset assignment. Preview uses the same `PopupTransitionPlayer` implementation as runtime.

## Framework layout

```text
Runtime/        Player/runtime code, including Graphics, Diagnostics and Extensions
Editor/         Setup, validation and inspectors
Tests/          Framework contract tests
ThirdParty/     Bundled VContainer, UniTask, private Json.NET 13.0.4 build and Safe Area utility
Samples~/       Optional Package Manager samples, including the Safe Area demo
Documentation~/ Detailed documentation
.github/        Repository validation workflow
```

## Documentation

Start with `Documentation~/index.md` and `Documentation~/getting-started.md`. The documentation covers the current Assets/Plugins workflow, scoped architecture, Safe Area utility, non-destructive Setup/Repair, optional Input System integration, URP UI camera stacking, and BasePopup animation preview.

## Validation

Repository validation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .github/scripts/Validate-Package.ps1
```

This checks package identity, folder hygiene, Unity metas/GUIDs, asmdef references/cycles, serialized script GUIDs and architecture regressions.
