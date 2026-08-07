using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Base.Assets
{
    /// <summary>
    /// Backward-compatible alias for projects that previously referenced this type.
    /// The core package no longer hard-depends on Addressables; use ResourcesAssetProvider by
    /// default or provide an Addressables implementation from a separate integration assembly.
    /// </summary>
    [Obsolete(
        "AddressablesAssetProvider in the core package now delegates to Resources. " +
        "Move a real Addressables provider into a separate integration assembly.")]
    public sealed class AddressablesAssetProvider : IAssetProvider, IDisposable
    {
        private readonly ResourcesAssetProvider fallback;

        public AddressablesAssetProvider(IObjectResolver objectResolver = null)
        {
            fallback = new ResourcesAssetProvider(objectResolver);
        }

        public UniTask<T> LoadAssetAsync<T>(
            string key,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            return fallback.LoadAssetAsync<T>(key, cancellationToken);
        }

        public UniTask<GameObject> InstantiateAsync(
            string key,
            Transform parent = null,
            CancellationToken cancellationToken = default)
        {
            return fallback.InstantiateAsync(key, parent, cancellationToken);
        }

        public UniTask<T> InstantiateAsync<T>(
            string key,
            Transform parent = null,
            CancellationToken cancellationToken = default)
            where T : Component
        {
            return fallback.InstantiateAsync<T>(key, parent, cancellationToken);
        }

        public void ReleaseAsset<T>(T asset) where T : UnityEngine.Object
        {
            fallback.ReleaseAsset(asset);
        }

        public void ReleaseInstance(GameObject instance)
        {
            fallback.ReleaseInstance(instance);
        }

        public void Dispose()
        {
            fallback.Dispose();
        }
    }
}
