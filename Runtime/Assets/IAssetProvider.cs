using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace HP.Framework.Assets
{
    /// <summary>
    /// v3 compatibility contract for async asset loading and instantiation using UniTask.
    /// Hides Addressables / Resources implementation details from domain and presentation layers.
    /// New ownership-sensitive code should prefer IAssetLeaseProvider when the provider supports it.
    /// </summary>
    public interface IAssetProvider
    {
        UniTask<T> LoadAssetAsync<T>(string key, CancellationToken cancellationToken = default) where T : UnityEngine.Object;
        UniTask<GameObject> InstantiateAsync(string key, Transform parent = null, CancellationToken cancellationToken = default);
        UniTask<T> InstantiateAsync<T>(string key, Transform parent = null, CancellationToken cancellationToken = default) where T : Component;

        void ReleaseAsset<T>(T asset) where T : UnityEngine.Object;
        void ReleaseInstance(GameObject instance);
    }
}


