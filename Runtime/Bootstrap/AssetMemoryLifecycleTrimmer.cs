using System;
using HP.Framework.Assets;
using HP.Framework.Lifecycle;
using VContainer.Unity;

namespace HP.Framework.Bootstrap
{
    /// <summary>
    /// Composition adapter between application lifecycle and an ownership-aware asset provider.
    /// Lifecycle stays independent from Assets; Assets stays independent from Lifecycle.
    /// </summary>
    internal sealed class AssetMemoryLifecycleTrimmer : IInitializable, IDisposable
    {
        private readonly IApplicationLifecycle lifecycle;
        private readonly IAssetMemoryService assetMemory;
        private bool initialized;

        public AssetMemoryLifecycleTrimmer(
            IApplicationLifecycle lifecycle,
            IAssetMemoryService assetMemory)
        {
            this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            this.assetMemory = assetMemory ?? throw new ArgumentNullException(nameof(assetMemory));
        }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            lifecycle.LowMemory += OnLowMemory;
            initialized = true;
        }

        public void Dispose()
        {
            if (!initialized)
            {
                return;
            }

            lifecycle.LowMemory -= OnLowMemory;
            initialized = false;
        }

        private void OnLowMemory()
        {
            assetMemory.TrimUnused();
        }
    }
}
