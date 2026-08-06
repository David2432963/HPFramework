using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
#if ADDRESSABLES_PRESENT
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace Base.Assets
{
    /// <summary>
    /// Asset provider implementation powered by Addressables and UniTask.
    /// Supports conditional compilation if Addressables package is installed, with Resources fallback.
    /// </summary>
    public sealed class AddressablesAssetProvider : IAssetProvider
    {
        private readonly IObjectResolver objectResolver;
        private readonly Dictionary<string, UnityEngine.Object> loadedAssets = new Dictionary<string, UnityEngine.Object>();
        private readonly Dictionary<GameObject, string> instanceToKeyMap = new Dictionary<GameObject, string>();

        public AddressablesAssetProvider(IObjectResolver objectResolver = null)
        {
            this.objectResolver = objectResolver;
        }

        public async UniTask<T> LoadAssetAsync<T>(string key, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                BaseLog.LogError("[AddressablesAssetProvider] Asset key is null or empty.");
                return null;
            }

            if (loadedAssets.TryGetValue(key, out var cached) && cached is T typedCached)
            {
                return typedCached;
            }

#if ADDRESSABLES_PRESENT
            var handle = Addressables.LoadAssetAsync<T>(key);
            var asset = await handle.WithCancellation(cancellationToken);
            if (asset != null)
            {
                loadedAssets[key] = asset;
            }
            return asset;
#else
            var request = Resources.LoadAsync<T>(key);
            await request.WithCancellation(cancellationToken);
            var loaded = request.asset as T;
            if (loaded != null)
            {
                loadedAssets[key] = loaded;
            }
            return loaded;
#endif
        }

        public async UniTask<GameObject> InstantiateAsync(string key, Transform parent = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

#if ADDRESSABLES_PRESENT
            var handle = Addressables.InstantiateAsync(key, parent);
            var instance = await handle.WithCancellation(cancellationToken);
            if (instance != null)
            {
                if (objectResolver != null)
                {
                    objectResolver.InjectGameObject(instance);
                }
                instanceToKeyMap[instance] = key;
            }
            return instance;
#else
            var prefab = await LoadAssetAsync<GameObject>(key, cancellationToken);
            if (prefab == null) return null;

            GameObject instance;
            if (objectResolver != null)
            {
                instance = objectResolver.Instantiate(prefab, parent);
            }
            else
            {
                instance = Object.Instantiate(prefab, parent, false);
            }

            if (instance != null)
            {
                instanceToKeyMap[instance] = key;
            }
            return instance;
#endif
        }

        public async UniTask<T> InstantiateAsync<T>(string key, Transform parent = null, CancellationToken cancellationToken = default) where T : Component
        {
            var instance = await InstantiateAsync(key, parent, cancellationToken);
            return instance != null ? instance.GetComponent<T>() : null;
        }

        public void ReleaseAsset<T>(T asset) where T : UnityEngine.Object
        {
            if (asset == null) return;

#if ADDRESSABLES_PRESENT
            Addressables.Release(asset);
#else
            Resources.UnloadUnusedAssets();
#endif
        }

        public void ReleaseInstance(GameObject instance)
        {
            if (instance == null) return;

#if ADDRESSABLES_PRESENT
            Addressables.ReleaseInstance(instance);
#else
            Object.Destroy(instance);
#endif
        }
    }
}
