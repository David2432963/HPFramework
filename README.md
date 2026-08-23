# HP Framework

HP Framework is a VContainer-first Unity 6 game foundation built around explicit lifetime ownership, persistent application services, async application readiness, mobile performance policy, scene/feature scopes, scoped UI/pooling/events, and reusable runtime utilities.

During active development, the framework is intended to live directly under `Assets/Plugins/HPFramework` so its source remains editable inside the consuming Unity project. The repository keeps a UPM-compatible layout for future Git/local Package Manager distribution.

## Requirements

- Unity 6 (`6000.0` or newer)
- UGUI is required by the current UI/Graphics/Diagnostics stack
- Input System and UGUI are required by the zero-setup canonical Bootstrap and are declared through `package.json`
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
│   ├── Bootstrap/Prefabs/Bootstrap.prefab
│   └── Defaults/
├── Editor/
├── Tests/
├── ThirdParty/
├── Samples~/
├── Documentation~/
└── package.json
```

The canonical Bootstrap and framework defaults are Git-tracked with stable GUIDs. A fresh clone does not generate framework assets per machine.

## Quick start

1. Place `Runtime/Bootstrap/Prefabs/Bootstrap.prefab` in the application entry scene.
2. Keep project-specific customization as prefab-instance overrides in that scene.
3. Press Play. The canonical Bootstrap already references valid default Audio/UI/Input/Performance assets.
4. Optionally run `Tools > HP Framework > Setup` for validation/repair or project integration diagnostics.
5. Keep application/game code outside the framework repository, for example under `Assets/Game`.

The Editor integration automatically adds and enables the framework-owned `LoadingScene` in Build Settings when needed. Customize
its neutral UGUI presentation directly at
`Runtime/Bootstrap/Loading/Scenes/LoadingScene.unity`; keep the scene name unchanged so the
default `GameSceneManager` configuration can load it.

Normal **Repair** is non-destructive: it fills missing objects/references while preserving existing manager ownership, camera settings, Canvas Scaler values, layout, catalogs, Input Actions and Toast assignments. On a scene prefab instance, project overrides remain on the instance and are never applied back to HP Framework automatically.

Use **Reset Canonical Bootstrap To Framework Defaults** only when you intentionally want to restore the framework-owned prefab itself.

HP Framework ships these canonical assets directly:

```text
Assets/Plugins/HPFramework/Runtime/
├── Bootstrap/Prefabs/
│   └── Bootstrap.prefab
└── Defaults/
    ├── VContainerSettings.asset
    ├── DefaultAudioLibrary.asset
    ├── DefaultUICatalog.asset
    └── Performance/
        ├── Low.asset
        ├── Medium.asset
        ├── High.asset
        ├── PerformanceCatalog.asset
        └── DevicePerformancePolicy.asset
```

Framework assets must never reference consuming-project assets. Projects customize Bootstrap through scene/prefab-instance overrides.

## Default Bootstrap

The canonical Bootstrap is organized by ownership domain rather than putting every manager on the root object:

```text
Bootstrap                         RootLifetimeScope
├── Lifecycle                     ApplicationLifecycleService
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

`Bootstrap` is the composition root. `ApplicationLifecycleService` centralizes pause/resume, focus, low-memory and quit signals without an idle `Update()` loop. Runtime-owned children such as SFX channels or pooled instances live under their owning domain instead of cluttering the root hierarchy.

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

Framework runtime code avoids `LifetimeScope.Find<T>()` and `.Container.Resolve<T>()` service-locator patterns. Prefer constructor/method injection and VContainer lifecycle entry points. Async application/content/platform readiness is coordinated separately by `IStartupCoordinator`; synchronous DI wiring stays in `IInitializable`. Bootstrap registers the coordinator but the application boot flow explicitly starts `RunAsync()` so container construction is not treated as application readiness.

Runtime frame-rate and Unity Quality actuation is owned by `PerformanceService`; Settings persists user/auto preferences only. See `Documentation~/performance.md` for the apply order, frame-pacing policy and extension hooks.

Audio playback uses a hard SFX voice ceiling. `AudioLibrarySO` entries can keep direct clips or opt into asset-key loading; active asset-backed voices and music retain their lease until stopped or replaced. See `Documentation~/audio.md`.

Additional input maps support explicit `IInputMapLease` ref-count ownership. Use leases whenever more than one feature may require the same overlay map; primary maps retain switch semantics. See `Documentation~/input.md`.

The opt-in mobile diagnostics HUD samples FPS, frame time, memory and framework service snapshots at a throttled interval. It is enabled in Editor/Development builds and excluded from production behavior unless explicitly opted in. See `Documentation~/mobile-diagnostics.md`.

Haptics expose semantic feedback (`Selection`, impacts, success/warning/error) through platform backends while retaining the duration-based compatibility API. See `Documentation~/haptics.md`.

## UI runtime notes

When URP is present, HP Framework configures `Camera.main` as a Base camera and the framework `UICamera` as Overlay, then attaches the UI camera to the active main-camera stack without duplicate entries. The gameplay camera must use the `MainCamera` tag.

`BasePopup` supports edit-mode transition preview using the same `PopupTransitionPlayer` evaluator as runtime.

## Performance characteristics

The framework favors predictable hot paths rather than hidden per-frame work:

- pooled instances cache their `IPoolable` components instead of rescanning hierarchies on every spawn/release
- ownership-aware `IAssetLeaseProvider` requests share same-key loads, reference-count active owners, isolate waiter cancellation, and trim only zero-reference records
- JSON operations targeting the same save path are serialized and path traversal outside `Application.persistentDataPath` is rejected
- physics helpers expose non-alloc query overloads
- `CameraUtils` exposes caller-owned frustum-plane buffers for bulk visibility checks
- Ripple and normal scaled-time Bubbling Surface animation advance from the GPU shader clock instead of pushing effect time through a `MaterialPropertyBlock` every frame
- Safe Area and Diagnostics avoid unnecessary every-frame UI/property updates

See [Runtime performance](Documentation~/runtime-performance.md) for usage patterns and trade-offs, and [Assets and ownership](Documentation~/assets.md) for provider/lease lifetime semantics.

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
- [Audio](Documentation~/audio.md)
- [Input](Documentation~/input.md)
- [Mobile diagnostics](Documentation~/mobile-diagnostics.md)
- [Haptics](Documentation~/haptics.md)
- [Mobile foundation](Documentation~/mobile-foundation.md)
- [Runtime performance](Documentation~/runtime-performance.md)
- [Troubleshooting](Documentation~/troubleshooting.md)

## AI agent rules and knowledge

For projects that use AI coding agents, the companion repository below contains reusable agent rules and modular knowledge files for Unity project implementation:

- [HPFramework-Agent-Rules](https://github.com/David2432963/HPFramework-Agent-Rules)

Keep framework usage details in the HP Framework documentation above. The companion rules repository is intended to guide how an AI agent works inside a project and direct it to the relevant framework documentation when needed, rather than duplicating the framework documentation itself.

## Validation

Run repository validation from the framework root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .github/scripts/Validate-Package.ps1
```

The validator checks package identity, folder hygiene, Unity metas/GUIDs, asmdef references/cycles, serialized script GUIDs and framework architecture regressions.

## Distribution status

The repository remains UPM-compatible through `package.json`, but editable `Assets/Plugins` consumption is the intended workflow during active development.

Before public redistribution, choose explicit HP Framework license terms and verify the redistribution terms for the retained Safe Area helper. Bundled dependency notices are documented in `THIRD PARTY NOTICES.md`.
