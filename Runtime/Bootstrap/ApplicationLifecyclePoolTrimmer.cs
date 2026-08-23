using System;
using HP.Framework.Lifecycle;
using HP.Framework.Pooling;
using VContainer.Unity;

namespace HP.Framework.Bootstrap
{
    internal sealed class ApplicationLifecyclePoolTrimmer : IInitializable, IDisposable
    {
        private readonly IApplicationLifecycle lifecycle;
        private readonly IPoolService poolService;
        private bool initialized;

        public ApplicationLifecyclePoolTrimmer(
            IApplicationLifecycle lifecycle,
            IPoolService poolService)
        {
            this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            this.poolService = poolService ?? throw new ArgumentNullException(nameof(poolService));
        }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            lifecycle.LowMemory += TrimSafely;
            initialized = true;
        }

        public void Dispose()
        {
            if (!initialized)
            {
                return;
            }

            lifecycle.LowMemory -= TrimSafely;
            initialized = false;
        }

        private void TrimSafely()
        {
            try
            {
                poolService.TrimAll(PoolTrimPolicy.Aggressive);
            }
            catch (Exception exception)
            {
                BaseLog.LogError($"[Lifecycle] Failed to trim inactive pools: {exception}");
            }
        }
    }
}
