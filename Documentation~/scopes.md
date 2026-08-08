# Lifetime scopes

Use `RootLifetimeScope` once as the VContainer project root. HP Framework setup registers the generated Bootstrap through `VContainerSettings` in Preloaded Assets.

Use `BaseSceneLifetimeScope` for menu/gameplay scenes and `BaseFeatureLifetimeScope` for shorter-lived feature graphs. Child scopes inherit parent registrations and dispose their own registrations when destroyed.

VContainer's `Lifetime.Scoped` is scoped per container, so resolving an inherited scoped registration from a feature child creates that feature's scoped instance. If a service must be owned by the scene and shared unchanged with all feature children, register it as `Lifetime.Singleton` inside the scene scope; it is still disposed when that scene container is disposed.

Scoped infrastructure follows the same ownership rule. `RegisterScopedUI()` and `RegisterScopedPool()` create child-owned UI/pool services. `RegisterScopeEventBus()` creates one `IEventBus` in the current container; because it is registered as a singleton inside that container, children inherit the same instance unless they register their own bus, and the owner container disposes it.

Avoid resolving services through global container lookups. Constructor/method injection and VContainer entry points keep ownership explicit.
