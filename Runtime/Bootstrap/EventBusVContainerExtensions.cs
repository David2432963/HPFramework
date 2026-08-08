using HP.Framework.Common;
using VContainer;

namespace HP.Framework.Bootstrap
{
    public static class EventBusVContainerExtensions
    {
        /// <summary>
        /// Registers an EventBus owned by the current LifetimeScope. Lifetime.Singleton is
        /// intentional: it creates one bus for this container and that instance is disposed
        /// with the container. Child scopes inherit it unless they register their own bus.
        /// </summary>
        public static void RegisterScopeEventBus(this IContainerBuilder builder)
        {
            builder.Register<EventBus>(Lifetime.Singleton)
                .As<IEventBus>()
                .As<System.IDisposable>();
        }
    }
}
