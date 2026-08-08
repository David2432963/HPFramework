using VContainer;
using VContainer.Unity;

namespace HP.Framework.Bootstrap
{
    /// <summary>
    /// Common composition pattern for child scopes owned by a scene or feature.
    /// The framework intentionally exposes VContainer's IContainerBuilder directly instead of
    /// wrapping registration behind another DI abstraction.
    /// </summary>
    public abstract class BaseChildLifetimeScope : LifetimeScope
    {
        protected sealed override void Configure(IContainerBuilder builder)
        {
            RegisterServices(builder);
            RegisterComponents(builder);
            RegisterEntryPoints(builder);
            ConfigureScope(builder);
        }

        /// <summary>
        /// Register plain C# services owned by this scope. Prefer Lifetime.Scoped for mutable
        /// scene/feature state that must be shared by consumers in the same scope.
        /// </summary>
        protected virtual void RegisterServices(IContainerBuilder builder)
        {
        }

        /// <summary>
        /// Register scene/feature MonoBehaviours that genuinely need Unity objects, hierarchy,
        /// serialization, or Unity callbacks.
        /// </summary>
        protected virtual void RegisterComponents(IContainerBuilder builder)
        {
        }

        /// <summary>
        /// Register VContainer lifecycle entry points such as IStartable, IAsyncStartable,
        /// ITickable, IFixedTickable, ILateTickable, and IDisposable owners.
        /// </summary>
        protected virtual void RegisterEntryPoints(IContainerBuilder builder)
        {
        }

        /// <summary>
        /// Escape hatch for uncommon registrations. Prefer the focused hooks above first.
        /// </summary>
        protected virtual void ConfigureScope(IContainerBuilder builder)
        {
        }
    }

    /// <summary>
    /// Base scope for scene-owned dependencies. With VContainerSettings configured, a scene
    /// scope automatically becomes a child of the project RootLifetimeScope unless another
    /// parent is explicitly assigned.
    /// </summary>
    public class BaseSceneLifetimeScope : BaseChildLifetimeScope
    {
    }

    /// <summary>
    /// Base scope for a shorter-lived feature inside a scene (level session, minigame, combat,
    /// tutorial, etc.). Prefer creating it through parentScope.CreateChild or assigning an
    /// explicit parent reference so it cannot accidentally bypass scene-owned dependencies.
    /// </summary>
    public class BaseFeatureLifetimeScope : BaseChildLifetimeScope
    {
    }
}


