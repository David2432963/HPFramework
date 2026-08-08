using System;
using System.Collections.Generic;
using UnityEngine;

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
            private readonly int capacity;
            private readonly Stack<GameObject> inactive = new Stack<GameObject>();
            private readonly HashSet<GameObject> inactiveSet = new HashSet<GameObject>();
            private readonly HashSet<GameObject> allInstances = new HashSet<GameObject>();
            private readonly Dictionary<GameObject, IPoolable[]> poolablesByInstance =
                new Dictionary<GameObject, IPoolable[]>();
            private readonly Func<GameObject, Transform, GameObject> instantiate;

            public GameObjectPool(
                GameObject prefab,
                Transform poolParent,
                int capacity,
                Func<GameObject, Transform, GameObject> instantiate)
            {
                this.prefab = prefab;
                this.poolParent = poolParent;
                this.capacity = Mathf.Max(1, capacity);
                this.instantiate = instantiate;
            }

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

            public IEnumerable<GameObject> Prewarm(int count)
            {
                int createCount = Mathf.Max(
                    0,
                    Mathf.Min(count, capacity) - inactiveSet.Count);
                for (int i = 0; i < createCount; i++)
                {
                    GameObject instance = instantiate(prefab, poolParent);
                    if (instance == null)
                    {
                        throw new InvalidOperationException(
                            $"Failed to prewarm pooled prefab '{prefab.name}'.");
                    }

                    allInstances.Add(instance);
                    CachePoolables(instance);
                    InvokePoolables(instance, spawning: false);
                    instance.SetActive(false);
                    inactive.Push(instance);
                    inactiveSet.Add(instance);
                    yield return instance;
                }
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
                    UnityEngine.Object.Destroy(instance);
                    return false;
                }

                instance.transform.SetParent(poolParent, false);
                inactive.Push(instance);
                inactiveSet.Add(instance);
                return true;
            }

            public void Clear()
            {
                foreach (GameObject instance in allInstances)
                {
                    if (instance != null)
                    {
                        UnityEngine.Object.Destroy(instance);
                    }
                }

                allInstances.Clear();
                inactive.Clear();
                inactiveSet.Clear();
                poolablesByInstance.Clear();

                if (poolParent != null)
                {
                    UnityEngine.Object.Destroy(poolParent.gameObject);
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
            foreach (GameObject instance in pool.Prewarm(count))
            {
                instanceToPool[instance] = pool;
            }
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

        private GameObjectPool GetOrCreatePool(GameObject prefab)
        {
            if (pools.TryGetValue(prefab, out GameObjectPool pool))
            {
                return pool;
            }

            GameObject parentObject = new GameObject($"Pool_{prefab.name}");
            parentObject.transform.SetParent(rootPoolParent, false);
            pool = new GameObjectPool(
                prefab,
                parentObject.transform,
                maxInactiveInstancesPerPool,
                instantiate);
            pools.Add(prefab, pool);
            return pool;
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


