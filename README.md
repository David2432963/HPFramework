# HP Framework

HP Framework is a VContainer-first Unity 6 game foundation built around explicit lifetime ownership, persistent application services, scene/feature scopes, scoped UI/pooling/events, and reusable runtime utilities.

During active development, the framework is intended to live directly under `Assets/Plugins/HPFramework` so its source remains editable inside the consuming Unity project. The repository keeps a UPM-compatible layout for future Git/local Package Manager distribution.

## Requirements

- Unity 6 (`6000.0` or newer)
- UGUI is required by the current UI/Graphics/Diagnostics stack
- Input System is optional while the framework is consumed as editable source under `Assets/Plugins`; `HP.Framework.Input` is excluded when `com.unity.inputsystem` is absent
- Future UPM installs declare Input System and UGUI through `package.json`
- VContainer, UniTask and a private Json.NET 13.0.4 build are bundled under `ThirdParty/`

## Development install

Clone or copy the framework repository to:

```text
Assets/Plugins/HPFramework/
```

The framework keeps its asmdef boundaries and may keep its own nested Git repository while remaining fully editable from the Unity project.

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

`Generated/` and `Settings/` are host-project outputs created by Setup and are intentionally excluded from the framework Git repository.

## Quick start

1. Open `Tools > HP Framework > Setup`.
2. Run **Setup / Repair Missing References**.
3. Run **Validate Project**.
4. Keep application/game code outside the framework repository, for example under `Assets/Game`.

Normal **Repair** is non-destructive: it fills missing objects/references while preserving existing manager ownership, camera settings, Canvas Scaler values, layout, catalogs, Input Actions and Toast assignments.

Use **Reset Bootstrap To Framework Defaults** only when you intentionally want the canonical HP Framework hierarchy and defaults restored.

Setup creates or repairs:

```text
Assets/Plugins/HPFramework/
├── Generated/
│   └── Bootstrap.prefab
└── Settings/
    ├── VContainerSettings.asset
    ├── DefaultAudioLibrary.asset
    └── DefaultUICatalog.asset
```

The reusable Bootstrap source remains tracked at `Editor/Templates/Bootstrap.prefab`.

## Default Bootstrap

The canonical Bootstrap is organized by ownership domain rather than putting every manager on the root object:

```text
Bootstrap                         RootLifetimeScope
├── Audio                         AudioManager
├── UI                            UIManager
│   ├── UICamera
│   ├── UICanvas
│   │   ├── ScreenRoot
│   │   ├── PopupRoot
│   │   ├── NotificationRoot
│   │   └── InputBlocker
│   └── EventSystem
├── Input                         InputManager when available
├── Scene                         GameSceneManager
├── Pools                         PoolManager
└── Haptics                       HapticManager
```

`Bootstrap` is the composition root. Runtime-owned children such as SFX channels or pooled instances live under their owning domain instead of cluttering the root hierarchy.

When Input System is available, Setup configures the project-appropriate EventSystem input module. When URP is available, Setup/runtime configure the framework UI camera for Base/Overlay stacking without making the reusable template hard-depend on URP camera data.

## Architecture

```text
RootLifetimeScope                  application lifetime
└── BaseSceneLifetimeScope         menu/gameplay lifetime
    └── BaseFeatureLifetimeScope   short feature lifetime
```

Use the root for application-wide services only. Put mutable gameplay state in scene/feature scopes.

A scene or feature can shadow infrastructure with its own resolver and lifetime:

```csharp
protected override void RegisterServices(IContainerBuilder builder)
{
    builder.RegisterScopeEventBus();
    builder.RegisterScopedPool();
    builder.RegisterScopedUI();
}
```

Children inherit parent registrations unless they intentionally shadow them. Prefer the scope-owned `IEventBus` for new code; the static `Observer` API remains for compatibility/global convenience.

A service that must be scene-owned and shared as the exact same instance by all feature children should be registered as `Lifetime.Singleton` inside the scene scope. Use `Lifetime.Scoped` when each child container should intentionally receive its own instance.

Framework runtime code avoids `LifetimeScope.Find<T>()` and `.Container.Resolve<T>()` service-locator patterns. Prefer constructor/method injection and VContainer lifecycle entry points.

## UI runtime notes

When URP is present, HP Framework configures `Camera.main` as a Base camera and the framework `UICamera` as Overlay, then attaches the UI camera to the active main-camera stack without duplicate entries. The gameplay camera must use the `MainCamera` tag.

`BasePopup` supports edit-mode transition preview using the same `PopupTransitionPlayer` evaluator as runtime.

## Performance characteristics

The framework favors predictable hot paths rather than hidden per-frame work:

- pooled instances cache their `IPoolable` components instead of rescanning hierarchies on every spawn/release
- same-key `ResourcesAssetProvider` requests are serialized so only one Resources load is performed before later callers reuse the cache
- JSON operations targeting the same save path are serialized and path traversal outside `Application.persistentDataPath` is rejected
- physics helpers expose non-alloc query overloads
- `CameraUtils` exposes caller-owned frustum-plane buffers for bulk visibility checks
- Ripple and normal scaled-time Bubbling Surface animation advance from the GPU shader clock instead of pushing effect time through a `MaterialPropertyBlock` every frame
- Safe Area and Diagnostics avoid unnecessary every-frame UI/property updates

See [Runtime performance](Documentation~/runtime-performance.md) for usage patterns and trade-offs.

## Framework layout

```text
Runtime/        Player/runtime modules
Editor/         Setup, repair, validation and inspectors
Tests/          Framework contract/regression tests
ThirdParty/     Bundled VContainer, UniTask, private Json.NET and Safe Area utility
Samples~/       Optional samples
Documentation~/ Detailed architecture and usage documentation
.github/        Repository validation/release workflow
```

## Documentation

Start with:

- [Documentation index](Documentation~/index.md)
- [Installation](Documentation~/installation.md)
- [Getting started](Documentation~/getting-started.md)
- [Architecture](Documentation~/architecture.md)
- [Lifetime scopes](Documentation~/scopes.md)
- [UI and pooling](Documentation~/ui-and-pooling.md)
- [Runtime performance](Documentation~/runtime-performance.md)
- [Troubleshooting](Documentation~/troubleshooting.md)

## Validation

Run repository validation from the framework root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .github/scripts/Validate-Package.ps1
```

The validator checks package identity, folder hygiene, Unity metas/GUIDs, asmdef references/cycles, serialized script GUIDs and framework architecture regressions.

## Distribution status

The repository remains UPM-compatible through `package.json`, but editable `Assets/Plugins` consumption is the intended workflow during active development.

Before public redistribution, choose explicit HP Framework license terms and verify the redistribution terms for the retained Safe Area helper. Bundled dependency notices are documented in `THIRD PARTY NOTICES.md`.
