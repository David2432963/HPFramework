# Architecture

HP Framework uses explicit dependency ownership:

```text
RootLifetimeScope          application lifetime
└── BaseSceneLifetimeScope scene/menu/gameplay lifetime
    └── BaseFeatureLifetimeScope short feature lifetime
```

The root registers shared assets, settings, application procedures, a root `IEventBus`, and persistent managers. Scene and feature scopes inherit parent registrations but own and dispose scoped state.

Runtime assemblies are split by responsibility. `HP.Framework.Core` has no assembly references. Optional integrations should live outside the core package rather than adding SDK dependencies to the root graph.

`ApplicationProcedure` coordinates application flow. Gameplay update loops and mutable gameplay state belong in scene/feature services or VContainer entry points.

For new event-driven code prefer `IEventBus`. The root bus is inherited by children by default. A scene or feature can call `RegisterScopeEventBus()` to shadow it with a bus disposed together with that container. The legacy static `Observer` remains available for compatibility, but it is not scope-owned.

## Runtime ownership rules

- Application-wide services belong to the root container.
- Menu/gameplay state belongs to scene containers.
- Short-lived feature state belongs to feature containers.
- A service that must be scene-owned and shared as the exact same instance by all feature children should be registered as `Lifetime.Singleton` inside the scene container.
- Use `Lifetime.Scoped` when each child container should intentionally receive its own scoped instance.

Framework runtime code avoids `LifetimeScope.Find<T>()` and `.Container.Resolve<T>()` service-locator patterns. Prefer constructor/method injection and VContainer lifecycle entry points.
