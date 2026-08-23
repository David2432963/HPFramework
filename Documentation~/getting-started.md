# Getting started

## 1. Install the editable framework

Place or clone HP Framework at:

```text
Assets/Plugins/HPFramework
```

Keep game-specific code outside the framework repository, for example under `Assets/Game`.

## 2. Use the canonical Bootstrap

Drag this Git-tracked prefab into the application entry scene:

```text
Assets/Plugins/HPFramework/Runtime/Bootstrap/Prefabs/Bootstrap.prefab
```

It already references valid default AudioLibrary, UICatalog, Input Actions and Performance assets. Project customization should be stored as prefab-instance overrides in the scene.

The Editor integration adds and enables the framework-owned loading scene in Build Settings when needed. Its default UGUI
layout can be customized at:

```text
Assets/Plugins/HPFramework/Runtime/Bootstrap/Loading/Scenes/LoadingScene.unity
```

Keep the scene named `LoadingScene`. An empty `GameSceneManager.loadingSceneName` explicitly
disables loading-scene presentation; any configured name must resolve to an enabled Build
Settings scene.

No per-machine Bootstrap/default generation is required. `Tools > HP Framework > Setup` remains available for optional validation and repair.

## 3. Understand Repair vs Reset

Normal **Repair** is non-destructive. It fills missing hierarchy objects/references while preserving existing ownership and user configuration.

Use **Reset Canonical Bootstrap To Framework Defaults** only when you intentionally want to restore the framework-owned canonical Bootstrap:

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

The root object is the composition root. `ApplicationLifecycleService` is repaired/created under `Lifecycle` and exposes normalized application pause/resume, focus, low-memory and quit signals. Runtime-created SFX channels and pooled instances belong under their owning domain rather than directly under `Bootstrap`.

## 4. Put code in the correct lifetime

Use `RootLifetimeScope` for application-owned services and entry points only.

Use `BaseSceneLifetimeScope` for menu/gameplay state. Use `BaseFeatureLifetimeScope` for shorter-lived feature graphs.

A scene/feature can own its own infrastructure:

```csharp
protected override void RegisterServices(IContainerBuilder builder)
{
    builder.RegisterScopeEventBus();
    builder.RegisterScopedPool();
    builder.RegisterScopedUI();
}
```

Child scopes inherit parent registrations unless they intentionally shadow them.

For a service that must be scene-owned and shared as the exact same instance by all feature children, register `Lifetime.Singleton` inside the scene scope. Use `Lifetime.Scoped` when each child should intentionally receive its own instance.

## Startup order

`RootLifetimeScope` inherits VContainer's early execution order (`-5000`). With `autoRun` enabled, its container is built from `Awake()`, and synchronous `IInitializable` services are dispatched during that build.

This is useful for synchronous framework bootstrapping, but raw `Awake()` ordering is not an async application-ready barrier. If the application later adds remote config, authentication, Addressables catalog loading, IAP initialization or similar async startup work, gate dependent flow through explicit lifecycle/readiness services.

## UI camera in URP

When URP is available, HP Framework configures the framework `UICamera` as Overlay, configures `Camera.main` as Base, and attaches the UI camera to the main-camera stack without duplicate entries.

The attachment is retried on scene load, so replacing the gameplay camera between Loading/Menu/Gameplay scenes is supported. The gameplay camera must use the `MainCamera` tag.

The canonical Bootstrap does not serialize URP camera data. Runtime/editor integration adds URP-specific camera data only when URP is present, keeping the prefab project-agnostic.

## Next steps

Read:

- [Architecture](architecture.md)
- [Lifetime scopes](scopes.md)
- [Assets and ownership](assets.md)
- [UI and pooling](ui-and-pooling.md)
- [Runtime performance](runtime-performance.md)
