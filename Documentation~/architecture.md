# Architecture

HP Framework uses explicit dependency ownership. The lifetime graph is the primary architecture rule:

```text
RootLifetimeScope                  application lifetime
└── BaseSceneLifetimeScope         scene/menu/gameplay lifetime
    └── BaseFeatureLifetimeScope   short feature lifetime
```

The root registers application-wide settings/assets/procedures, a root `IEventBus`, and persistent managers. In manual Bootstrap mode the root is placed in the entry scene and moves to `DontDestroyOnLoad`; `GameSceneManager` supplies that root as the VContainer parent while target scenes load. Scene and feature scopes inherit parent registrations but own and dispose their own state.

## Bootstrap as composition root

The canonical Bootstrap is organized by ownership domain:

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

`Bootstrap` should remain a composition root rather than a dumping ground for runtime children. Audio runtime channels belong under `Audio`; global pool contents belong under `Pools`; persistent UI belongs under `UI`.

Normal Setup/Repair preserves existing manager ownership. The explicit Reset action migrates/restores the canonical hierarchy.

## Runtime ownership rules

- Application-wide services belong to the root container.
- Menu/gameplay state belongs to scene containers.
- Short-lived feature state belongs to feature containers.
- A service that must be scene-owned and shared as the exact same instance by all feature children should be registered as `Lifetime.Singleton` inside the scene container. It is still disposed when that scene container is disposed.
- Use `Lifetime.Scoped` when each child container should intentionally receive its own scoped instance.
- Do not keep gameplay loops or mutable gameplay state in `ApplicationProcedure`; use scene/feature services or VContainer entry points.

## Scoped infrastructure

The root provides application-wide UI, pooling and event infrastructure. A child can intentionally shadow those registrations:

```csharp
protected override void RegisterServices(IContainerBuilder builder)
{
    builder.RegisterScopeEventBus();
    builder.RegisterScopedPool();
    builder.RegisterScopedUI();
}
```

`RegisterScopeEventBus()` owns one `IEventBus` in the current container. Children inherit it unless they register another bus.

`RegisterScopedPool()` creates a child-owned pool using that scope's resolver.

`RegisterScopedUI()` creates child-owned screens/popups using that scope's resolver while mounting them under the persistent UI roots.

For new event-driven code prefer `IEventBus`. The legacy static `Observer` remains available for compatibility/global convenience, but it is not scope-owned.

## Dependency resolution

Framework runtime code avoids service-locator patterns such as:

```csharp
LifetimeScope.Find<T>()
container.Resolve<T>()
```

inside ordinary runtime services.

Prefer constructor/method injection and VContainer lifecycle entry points so dependency ownership remains visible in the registration graph.

## Module boundaries

Runtime assemblies are split by responsibility. `HP.Framework.Core` is kept at the bottom of the dependency graph, while higher-level modules build on top of it.

Optional integrations should remain outside the core graph. The Input System integration is compile-time optional in editable Assets mode; Bootstrap/Editor setup resolves it without forcing the core architecture to depend on the package.

`ApplicationProcedure` coordinates application-level flow. It is not a substitute for scene gameplay systems.

## Performance as an architecture constraint

The framework avoids hidden hot-path work where practical: pooled component metadata is cached, resources same-key loads are deduplicated, persistence operations are serialized per path, non-alloc physics/frustum APIs are available, and supported surface effects use GPU clocks instead of unnecessary per-frame property-block time pushes.

See [Runtime performance](runtime-performance.md) for the concrete contracts and recommended usage patterns.
