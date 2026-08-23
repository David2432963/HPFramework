using UnityEngine;

using System.Threading;
using Cysharp.Threading.Tasks;

namespace HP.Framework.Pooling
{
    /// <summary>
    /// Contract for GameObject and Component pooling.
    /// </summary>
    public interface IPoolService
    {
        GameObject Get(GameObject prefab, Transform parent = null);
        T Get<T>(T prefab, Transform parent = null) where T : Component;
        void Release(GameObject instance);
        void Release<T>(T instance) where T : Component;
        void Prewarm(GameObject prefab, int count);
        void Prewarm<T>(T prefab, int count) where T : Component;
        UniTask PrewarmAsync(
            GameObject prefab,
            int count,
            PoolPrewarmOptions options,
            CancellationToken cancellationToken = default);
        void SetMaxInactive(GameObject prefab, int maxInactiveCount);
        void Trim(GameObject prefab, int targetInactiveCount);
        void TrimAll(PoolTrimPolicy policy);
        void ClearAllPools();
    }
}

