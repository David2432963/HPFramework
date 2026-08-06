using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Assets
{
    /// <summary>
    /// Contract for async asset loading and instantiation using UniTask.
    /// Hides Addressables / Resources implementation details from domain and presentation layers.
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
