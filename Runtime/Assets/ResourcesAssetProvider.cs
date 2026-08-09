using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace HP.Framework.Assets
{
    /// <summary>
    /// Default asset provider with no Addressables dependency.
    /// Loaded Resources assets are cached until explicitly released.
    /// Concurrent requests for the same key share a per-key gate so the resource is loaded once.
    /// </summary>
    public sealed class ResourcesAssetProvider : IAssetProvider, IDisposable
    {
        private readonly IObjectResolver objectResolver;
        private readonly Dictionary<string, UnityEngine.Object> loadedAssets =
            new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
        private readonly Dictionary<UnityEngine.Object, string> assetKeys =
            new Dictionary<UnityEngine.Object, string>();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> loadLocks =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);
        private readonly HashSet<GameObject> instances = new HashSet<GameObject>();

        private bool disposed;

        public ResourcesAssetProvider(IObjectResolver objectResolver = null)
        {
            this.objectResolver = objectResolver;
        }

        public async UniTask<T> LoadAssetAsync<T>(
            string key,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Asset key must not be empty.", nameof(key));
            }

            SemaphoreSlim loadLock = GetLoadLock(key);
            await loadLock.WaitAsync(cancellationToken);
            try
            {
                ThrowIfDisposed();
                if (loadedAssets.TryGetValue(key, out UnityEngine.Object cached))
                {
                    if (cached is T typedCached)
                    {
                        return typedCached;
                    }

                    throw new InvalidOperationException(
                        $"Asset key '{key}' is cached as {cached.GetType().Name}, not {typeof(T).Name}.");
                }

                ResourceRequest request = Resources.LoadAsync<T>(key);
                while (!request.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                T asset = request.asset as T;
                if (asset == null)
                {
                    throw new InvalidOperationException(
                        $"Resource '{key}' could not be loaded as {typeof(T).Name}.");
                }

                loadedAssets[key] = asset;
                assetKeys[asset] = key;
                return asset;
            }
            finally
            {
                loadLock.Release();
            }
        }

        public async UniTask<GameObject> InstantiateAsync(
            string key,
            Transform parent = null,
            CancellationToken cancellationToken = default)
        {
            GameObject prefab = await LoadAssetAsync<GameObject>(key, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            GameObject instance = objectResolver != null
                ? objectResolver.Instantiate(prefab, parent)
                : UnityEngine.Object.Instantiate(prefab, parent, false);

            if (instance == null)
            {
                throw new InvalidOperationException($"Failed to instantiate resource '{key}'.");
            }

            instances.Add(instance);
            return instance;
        }

        public async UniTask<T> InstantiateAsync<T>(
            string key,
            Transform parent = null,
            CancellationToken cancellationToken = default)
            where T : Component
        {
            GameObject instance = await InstantiateAsync(key, parent, cancellationToken);
            T component = instance.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            ReleaseInstance(instance);
            throw new InvalidOperationException(
                $"Instantiated resource '{key}' does not contain component {typeof(T).Name}.");
        }

        public void ReleaseAsset<T>(T asset) where T : UnityEngine.Object
        {
            if (asset == null)
            {
                return;
            }

            if (assetKeys.TryGetValue(asset, out string key))
            {
                assetKeys.Remove(asset);
                loadedAssets.Remove(key);
            }

            if (!(asset is GameObject) && !(asset is Component))
            {
                Resources.UnloadAsset(asset);
            }
        }

        public void ReleaseInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            instances.Remove(instance);
            UnityEngine.Object.Destroy(instance);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (GameObject instance in instances)
            {
                if (instance != null)
                {
                    UnityEngine.Object.Destroy(instance);
                }
            }

            instances.Clear();
            foreach (UnityEngine.Object asset in loadedAssets.Values)
            {
                if (asset != null && !(asset is GameObject) && !(asset is Component))
                {
                    Resources.UnloadAsset(asset);
                }
            }

            loadedAssets.Clear();
            assetKeys.Clear();
        }

        private SemaphoreSlim GetLoadLock(string key)
        {
            return loadLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(ResourcesAssetProvider));
            }
        }
    }
}
