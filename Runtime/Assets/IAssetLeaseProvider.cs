using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace HP.Framework.Assets
{
    /// <summary>
    /// Ownership-aware asset provider surface added alongside the v3 compatibility API.
    /// Existing IAssetProvider implementations do not need to implement this interface.
    /// </summary>
    public interface IAssetLeaseProvider : IAssetProvider
    {
        UniTask<IAssetLease<T>> AcquireAsync<T>(
            string key,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object;

        UniTask<IInstanceLease<T>> InstantiateLeaseAsync<T>(
            string key,
            Transform parent = null,
            CancellationToken cancellationToken = default)
            where T : Component;
    }
}
