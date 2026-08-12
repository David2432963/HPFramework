# Lifetime scopes

Use `RootLifetimeScope` on the HP Framework `Bootstrap` prefab and place that prefab explicitly in the application entry scene. `RootLifetimeScope` moves the Bootstrap hierarchy to `DontDestroyOnLoad`, so the entry/loading scene can unload while application services remain alive. In this project, `VContainerSettings` remains a preloaded settings asset but its `RootLifetimeScope` reference must stay `None`, so VContainer does not auto-instantiate Bootstrap.

`GameSceneManager` loads target scenes while `LifetimeScope.EnqueueParent(rootLifetimeScope)` is active. A `BaseSceneLifetimeScope` in the target scene therefore builds as a child of the persistent application root and can inject root UI/audio/pool/settings services. A scene played directly in development should assign an explicit root parent; do not use hierarchy searches or static root access.

```text
Entry scene
└── RootLifetimeScope (DontDestroyOnLoad)
    └── BaseSceneLifetimeScope (loaded target scene)
        └── BaseFeatureLifetimeScope
```

Child scopes inherit parent registrations and dispose registrations owned by their own container when destroyed.

## Singleton vs Scoped inside child containers

VContainer's `Lifetime.Scoped` is scoped per container. If a feature child resolves an inherited scoped registration, that feature can receive its own scoped instance.

If a service must be owned by the scene and shared as the exact same instance with all feature children, register it as `Lifetime.Singleton` inside the scene container:

```csharp
protected override void RegisterServices(IContainerBuilder builder)
{
    builder.Register<GameplaySession>(Lifetime.Singleton);
}
```

The instance is still disposed with the scene container because the singleton belongs to that container rather than the application root.

Use `Lifetime.Scoped` when each child container should intentionally own a separate instance.

## Scoped infrastructure

A scene or feature can shadow application infrastructure:

```csharp
protected override void RegisterServices(IContainerBuilder builder)
{
    builder.RegisterScopeEventBus();
    builder.RegisterScopedPool();
    builder.RegisterScopedUI();
}
```

Behavior:

- `RegisterScopeEventBus()` creates one `IEventBus` owned by the current container. Descendants inherit it unless they register another bus.
- `RegisterScopedPool()` creates an `IPoolService` that instantiates through the current scope resolver and is disposed with the scope.
- `RegisterScopedUI()` creates an `IUIService`/`IScopedUIService` whose local screens/popups are instantiated with the current resolver and destroyed with the scope.

This allows scene/feature prefabs to receive scene/feature dependencies without falling back to a global resolver.

## Resolution rules

Prefer injection:

```csharp
public sealed class EnemySpawner
{
    private readonly IPoolService pool;

    public EnemySpawner(IPoolService pool)
    {
        this.pool = pool;
    }
}
```

Avoid global container lookups in ordinary runtime code. Framework validation guards against `LifetimeScope.Find<T>()` and direct `.Container.Resolve<T>()` usage in framework runtime assemblies.

## Practical ownership examples

Application root:

```text
settings
persistent audio
persistent global UI
application procedure
root event bus
scene flow
```

Gameplay scene:

```text
game session
level state
enemy systems
scene event bus (optional shadow)
scene-owned pools/UI (optional shadow)
```

Feature scope:

```text
short-lived minigame
modal feature flow
temporary combat subsystem
feature-specific UI/pools/events
```

When the owner scope is destroyed, state owned by that scope should disappear with it.
