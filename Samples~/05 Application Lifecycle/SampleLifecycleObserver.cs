using System;
using HP.Framework.Assets;
using HP.Framework.Lifecycle;
using HP.Framework.Pooling;
using VContainer.Unity;

namespace HP.Framework.Samples.ApplicationLifecycle
{
    public sealed class SampleLifecycleObserver : IInitializable, IDisposable
    {
        private readonly IApplicationLifecycle lifecycle;
        private readonly IAssetMemoryService assets;
        private readonly IPoolService pools;

        public SampleLifecycleObserver(
            IApplicationLifecycle lifecycle,
            IAssetMemoryService assets,
            IPoolService pools)
        {
            this.lifecycle = lifecycle;
            this.assets = assets;
            this.pools = pools;
        }

        public void Initialize()
        {
            lifecycle.LowMemory += OnLowMemory;
        }

        public void Dispose()
        {
            lifecycle.LowMemory -= OnLowMemory;
        }

        private void OnLowMemory()
        {
            pools.TrimAll(PoolTrimPolicy.Aggressive);
            assets.TrimUnused();
        }
    }
}
