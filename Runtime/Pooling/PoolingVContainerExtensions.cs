using System;
using VContainer;

namespace HP.Framework.Pooling
{
    /// <summary>
    /// VContainer registrations for scope-owned pooling.
    /// </summary>
    public static class PoolingVContainerExtensions
    {
        /// <summary>
        /// Shadows the root IPoolService inside the current child scope. Any consumer resolved
        /// from that scope receives a pool that instantiates with the same IObjectResolver.
        /// </summary>
        public static RegistrationBuilder RegisterScopedPool(
            this IContainerBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.Register<ScopedPoolService>(Lifetime.Scoped)
                .AsSelf()
                .As<IPoolService>()
                .As<IDisposable>();
        }
    }
}


