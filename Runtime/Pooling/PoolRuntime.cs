using System;
using System.Collections.Generic;
using UnityEngine;

using System.Threading;
using Cysharp.Threading.Tasks;

namespace HP.Framework.Pooling
{
    /// <summary>
    /// Shared pooling engine used by both the application pool and child-scope pools.
    /// The caller supplies the VContainer-aware instantiate delegate, so every pool creates
    /// objects with the resolver that owns that pool.
    /// </summary>
    internal sealed class PoolRuntime : IDisposable
    {
        private sealed class GameObjectPool
        {
            private readonly GameObject prefab;
            private readonly Transform poolParent;
            private int capacity;
            private readonly Stack<GameObject> inactive = new Stack<GameObject>();
            private readonly HashSet<GameObject> inactiveSet = new HashSet<GameObject>();
            private readonly HashSet<GameObject> allInstances = new HashSet<GameObject>();
            private readonly Dictionary<GameObject, IPoolable[]> poolablesByInstance =
                new Dictionary<GameObject, IPoolable[]>();
            private readonly Func<GameObject, Transform, GameObject> instantiate;
            private int createdTotal;
            private int trimmedTotal;

            public GameObjectPool(
                GameObject prefab,
                Transform poolParent,
                int capacity,
                Func<GameObject, Transform, GameObject> instantiate)
            {
                this.prefab = prefab;
                this.poolParent = poolParent;
                this.capacity = Mathf.Max(0, capacity);
                this.instantiate = instantiate;
            }

            public GameObject Prefab => prefab;
            public int Capacity => capacity;
            public int ActiveCount => allInstances.Count - inactiveSet.Count;
            public int InactiveCount => inactiveSet.Count;
            public int CreatedTotal => createdTotal;
            public int TrimmedTotal => trimmedTotal;

            public GameObject Get(Transform parent)
            {
                GameObject instance = null;
                while (inactive.Count > 0 && instance == null)
                {
                    instance = inactive.Pop();
                    inactiveSet.Remove(instance);
                }

                if (instance == null)
                {
                    instance = instantiate(prefab, parent);
                    if (instance == null)
                    {
                        throw new InvalidOperationException(
                            $"Failed to instantiate pooled prefab '{prefab.name}'.");
                    }

                    allInstances.Add(instance);
                    createdTotal++;
                    CachePoolables(instance);
                }
                else
                {
                    instance.transform.SetParent(parent, false);
                }

                instance.SetActive(true);
                InvokePoolables(instance, spawning: true);
                return instance;
            }

            public int GetPrewarmCreateCount(int count)
            {
                return Mathf.Max(
                    0,
                    Mathf.Min(count, capacity) - inactiveSet.Count);
            }

            public GameObject PrewarmOne()
            {
                GameObject instance = instantiate(prefab, poolParent);
                if (instance == null)
                {
                    throw new InvalidOperationException(
                        $"Failed to prewarm pooled prefab '{prefab.name}'.");
                }

                allInstances.Add(instance);
                createdTotal++;
                CachePoolables(instance);
                InvokePoolables(instance, spawning: false);
                instance.SetActive(false);
                inactive.Push(instance);
                inactiveSet.Add(instance);
                return instance;
            }

            public bool Release(GameObject instance)
            {
                if (instance == null || !allInstances.Contains(instance))
                {
                    return false;
                }

                if (inactiveSet.Contains(instance))
                {
                    BaseLog.LogWarning($"[Pool] Duplicate release ignored: {instance.name}");
                    return true;
                }

                InvokePoolables(instance, spawning: false);
                instance.SetActive(false);
                if (inactiveSet.Count >= capacity)
                {
                    allInstances.Remove(instance);
                    poolablesByInstance.Remove(instance);
                    trimmedTotal++;
                    DestroyOwnedObject(instance);
                    return false;
                }

                instance.transform.SetParent(poolParent, false);
                inactive.Push(instance);
                inactiveSet.Add(instance);
                return true;
            }

            public void SetCapacity(int maxInactiveCount, Action<GameObject> onTrimmed)
            {
                capacity = Mathf.Max(0, maxInactiveCount);
                Trim(capacity, onTrimmed);
            }

            public void Trim(int targetInactiveCount, Action<GameObject> onTrimmed)
            {
                int target = Mathf.Max(0, targetInactiveCount);
                while (inactive.Count > target)
                {
                    GameObject instance = inactive.Pop();
                    inactiveSet.Remove(instance);
                    allInstances.Remove(instance);
                    poolablesByInstance.Remove(instance);
                    onTrimmed?.Invoke(instance);
                    trimmedTotal++;
                    if (instance != null)
                    {
                        DestroyOwnedObject(instance);
                    }
                }
            }

            public PoolStats GetStats()
            {
                return new PoolStats(
                    prefab,
                    capacity,
                    ActiveCount,
                    InactiveCount,
                    createdTotal,
                    trimmedTotal);
            }

            public void Clear()
            {
                foreach (GameObject instance in allInstances)
                {
                    if (instance != null)
                    {
                        DestroyOwnedObject(instance);
                    }
                }

                allInstances.Clear();
                inactive.Clear();
                inactiveSet.Clear();
                poolablesByInstance.Clear();

                if (poolParent != null)
                {
                    DestroyOwnedObject(poolParent.gameObject);
                }
            }

            private void CachePoolables(GameObject instance)
            {
                MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
                int poolableCount = 0;
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is IPoolable)
                    {
                        poolableCount++;
                    }
                }

                if (poolableCount == 0)
                {
                    poolablesByInstance[instance] = Array.Empty<IPoolable>();
                    return;
                }

                IPoolable[] poolables = new IPoolable[poolableCount];
                int index = 0;
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is IPoolable poolable)
                    {
                        poolables[index++] = poolable;
                    }
                }

                poolablesByInstance[instance] = poolables;
            }

            private void InvokePoolables(GameObject instance, bool spawning)
            {
                if (!poolablesByInstance.TryGetValue(instance, out IPoolable[] poolables))
                {
                    CachePoolables(instance);
                    poolables = poolablesByInstance[instance];
                }

                for (int i = 0; i < poolables.Length; i++)
                {
                    IPoolable poolable = poolables[i];
                    if (poolable == null)
                    {
                        continue;
                    }

                    if (spawning)
                    {
                        poolable.OnSpawn();
                    }
                    else
                    {
                        poolable.OnDespawn();
                    }
                }
            }
        }

        private readonly Transform rootPoolParent;
        private readonly int maxInactiveInstancesPerPool;
        private readonly Func<GameObject, Transform, GameObject> instantiate;
        private readonly Dictionary<GameObject, GameObjectPool> pools =
            new Dictionary<GameObject, GameObjectPool>();
        private readonly Dictionary<GameObject, GameObjectPool> instanceToPool =
            new Dictionary<GameObject, GameObjectPool>();
        private readonly Dictionary<GameObject, int> capacityOverrides =
            new Dictionary<GameObject, int>();

        private bool disposed;

        public PoolRuntime(
            Transform rootPoolParent,
            int maxInactiveInstancesPerPool,
            Func<GameObject, Transform, GameObject> instantiate)
        {
            this.rootPoolParent = rootPoolParent
                ? rootPoolParent
                : throw new ArgumentNullException(nameof(rootPoolParent));
            this.maxInactiveInstancesPerPool =
                Mathf.Max(1, maxInactiveInstancesPerPool);
            this.instantiate = instantiate
                ?? throw new ArgumentNullException(nameof(instantiate));
        }

        public GameObject Get(GameObject prefab, Transform parent = null)
        {
            ThrowIfDisposed();
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            GameObjectPool pool = GetOrCreatePool(prefab);
            GameObject instance = pool.Get(parent);
            instanceToPool[instance] = pool;
            return instance;
        }

        public T Get<T>(T prefab, Transform parent = null) where T : Component
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            GameObject instance = Get(prefab.gameObject, parent);
            T component = instance.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            Release(instance);
            throw new InvalidOperationException(
                $"Pooled prefab '{prefab.name}' has no {typeof(T).Name} component.");
        }

        public void Release(GameObject instance)
        {
            ThrowIfDisposed();
            if (instance == null)
            {
                return;
            }

            if (!instanceToPool.TryGetValue(instance, out GameObjectPool pool))
            {
                BaseLog.LogWarning(
                    $"[Pool] Release ignored because '{instance.name}' is not owned by this pool service.");
                return;
            }

            if (!pool.Release(instance))
            {
                instanceToPool.Remove(instance);
            }
        }

        public void Prewarm(GameObject prefab, int count)
        {
            ThrowIfDisposed();
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (count <= 0)
            {
                return;
            }

            GameObjectPool pool = GetOrCreatePool(prefab);
            int createCount = pool.GetPrewarmCreateCount(count);
            for (int i = 0; i < createCount; i++)
            {
                GameObject instance = pool.PrewarmOne();
                instanceToPool[instance] = pool;
            }
        }

        public async UniTask PrewarmAsync(
            GameObject prefab,
            int count,
            PoolPrewarmOptions options,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (count <= 0)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            GameObjectPool pool = GetOrCreatePool(prefab);
            int createCount = pool.GetPrewarmCreateCount(count);
            int maxInstancesPerFrame = options.ResolveMaxInstancesPerFrame();
            for (int i = 0; i < createCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GameObject instance = pool.PrewarmOne();
                instanceToPool[instance] = pool;

                bool hasMore = i + 1 < createCount;
                if (hasMore && (i + 1) % maxInstancesPerFrame == 0)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
        }

        public void SetMaxInactive(GameObject prefab, int maxInactiveCount)
        {
            ThrowIfDisposed();
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            int capacity = Mathf.Max(0, maxInactiveCount);
            capacityOverrides[prefab] = capacity;
            if (pools.TryGetValue(prefab, out GameObjectPool pool))
            {
                pool.SetCapacity(capacity, RemoveTrimmedInstance);
            }
        }

        public void Trim(GameObject prefab, int targetInactiveCount)
        {
            ThrowIfDisposed();
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (pools.TryGetValue(prefab, out GameObjectPool pool))
            {
                pool.Trim(targetInactiveCount, RemoveTrimmedInstance);
            }
        }

        public void TrimAll(PoolTrimPolicy policy)
        {
            ThrowIfDisposed();
            foreach (GameObjectPool pool in pools.Values)
            {
                pool.Trim(
                    policy.GetTargetInactiveCount(pool.InactiveCount),
                    RemoveTrimmedInstance);
            }
        }

        public PoolServiceStats GetStats()
        {
            ThrowIfDisposed();
            int active = 0;
            int inactive = 0;
            int created = 0;
            int trimmed = 0;
            foreach (GameObjectPool pool in pools.Values)
            {
                active += pool.ActiveCount;
                inactive += pool.InactiveCount;
                created += pool.CreatedTotal;
                trimmed += pool.TrimmedTotal;
            }

            return new PoolServiceStats(pools.Count, active, inactive, created, trimmed);
        }

        public bool TryGetStats(GameObject prefab, out PoolStats stats)
        {
            ThrowIfDisposed();
            if (prefab != null && pools.TryGetValue(prefab, out GameObjectPool pool))
            {
                stats = pool.GetStats();
                return true;
            }

            stats = default;
            return false;
        }

        public void ClearAllPools()
        {
            if (disposed)
            {
                return;
            }

            foreach (GameObjectPool pool in pools.Values)
            {
                pool.Clear();
            }

            pools.Clear();
            instanceToPool.Clear();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            ClearAllPools();
            disposed = true;
        }

        internal static void DestroyOwnedObject(UnityEngine.Object instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(instance);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private GameObjectPool GetOrCreatePool(GameObject prefab)
        {
            if (pools.TryGetValue(prefab, out GameObjectPool pool))
            {
                return pool;
            }

            GameObject parentObject = new GameObject($"Pool_{prefab.name}");
            parentObject.transform.SetParent(rootPoolParent, false);
            int capacity = capacityOverrides.TryGetValue(prefab, out int overrideCapacity)
                ? overrideCapacity
                : maxInactiveInstancesPerPool;
            pool = new GameObjectPool(
                prefab,
                parentObject.transform,
                capacity,
                instantiate);
            pools.Add(prefab, pool);
            return pool;
        }

        private void RemoveTrimmedInstance(GameObject instance)
        {
            instanceToPool.Remove(instance);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(PoolRuntime));
            }
        }
    }
}
