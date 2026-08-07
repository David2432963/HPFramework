using System;
using System.Collections.Generic;
using Base;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Base.Pooling;

public sealed class PoolManager : MonoBehaviour, IPoolService, IInitializable, IDisposable
{
    private sealed class GameObjectPool
    {
        private readonly GameObject prefab;
        private readonly Transform poolParent;
        private readonly int capacity;
        private readonly Stack<GameObject> inactive = new Stack<GameObject>();
        private readonly HashSet<GameObject> inactiveSet = new HashSet<GameObject>();
        private readonly HashSet<GameObject> allInstances = new HashSet<GameObject>();
        private readonly Func<GameObject, Transform, GameObject> instantiate;

        public GameObjectPool(GameObject prefab, Transform poolParent, int capacity,
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
                    throw new InvalidOperationException($"Failed to instantiate '{prefab.name}'.");
                }
                allInstances.Add(instance);
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
            int createCount = Mathf.Max(0, Mathf.Min(count, capacity) - inactiveSet.Count);
            for (int i = 0; i < createCount; i++)
            {
                GameObject instance = instantiate(prefab, poolParent);
                if (instance == null)
                {
                    throw new InvalidOperationException($"Failed to prewarm '{prefab.name}'.");
                }

                allInstances.Add(instance);
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
            if (poolParent != null)
            {
                UnityEngine.Object.Destroy(poolParent.gameObject);
            }
        }

        private static void InvokePoolables(GameObject instance, bool spawning)
        {
            MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPoolable poolable)
                {
                    if (spawning) poolable.OnSpawn();
                    else poolable.OnDespawn();
                }
            }
        }
    }

    [SerializeField] private Transform rootPoolParent;
    [SerializeField, Min(1)] private int maxInactiveInstancesPerPool = 64;

    private readonly Dictionary<GameObject, GameObjectPool> pools =
        new Dictionary<GameObject, GameObjectPool>();
    private readonly Dictionary<GameObject, GameObjectPool> instanceToPool =
        new Dictionary<GameObject, GameObjectPool>();

    private IObjectResolver objectResolver;
    private bool initialized;

    [Inject]
    public void Construct(IObjectResolver resolver) => objectResolver = resolver;

    public void Initialize()
    {
        if (initialized) return;
        if (rootPoolParent == null)
        {
            GameObject root = new GameObject("PoolRoot");
            root.transform.SetParent(transform, false);
            rootPoolParent = root.transform;
        }
        initialized = true;
    }

    public GameObject Get(GameObject prefab, Transform parent = null)
    {
        if (prefab == null) throw new ArgumentNullException(nameof(prefab));
        EnsureInitialized();
        GameObjectPool pool = GetOrCreatePool(prefab);
        GameObject instance = pool.Get(parent);
        instanceToPool[instance] = pool;
        return instance;
    }

    public T Get<T>(T prefab, Transform parent = null) where T : Component
    {
        if (prefab == null) throw new ArgumentNullException(nameof(prefab));
        GameObject instance = Get(prefab.gameObject, parent);
        T component = instance.GetComponent<T>();
        if (component != null) return component;
        Release(instance);
        throw new InvalidOperationException(
            $"Pooled prefab '{prefab.name}' has no {typeof(T).Name} component.");
    }

    public void Release(GameObject instance)
    {
        if (instance == null) return;
        if (!instanceToPool.TryGetValue(instance, out GameObjectPool pool))
        {
            UnityEngine.Object.Destroy(instance);
            return;
        }

        if (!pool.Release(instance))
        {
            instanceToPool.Remove(instance);
        }
    }

    public void Release<T>(T instance) where T : Component
    {
        if (instance != null) Release(instance.gameObject);
    }

    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null) throw new ArgumentNullException(nameof(prefab));
        if (count <= 0) return;
        EnsureInitialized();
        GameObjectPool pool = GetOrCreatePool(prefab);
        foreach (GameObject instance in pool.Prewarm(count))
        {
            instanceToPool[instance] = pool;
        }
    }

    public void Prewarm<T>(T prefab, int count) where T : Component
    {
        if (prefab == null) throw new ArgumentNullException(nameof(prefab));
        Prewarm(prefab.gameObject, count);
    }

    public void ClearAllPools()
    {
        foreach (GameObjectPool pool in pools.Values) pool.Clear();
        pools.Clear();
        instanceToPool.Clear();
    }

    public void Dispose()
    {
        ClearAllPools();
        initialized = false;
    }

    private GameObjectPool GetOrCreatePool(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out GameObjectPool pool)) return pool;
        GameObject parentObject = new GameObject($"Pool_{prefab.name}");
        parentObject.transform.SetParent(rootPoolParent, false);
        pool = new GameObjectPool(prefab, parentObject.transform,
            maxInactiveInstancesPerPool, InstantiateWithVContainer);
        pools.Add(prefab, pool);
        return pool;
    }

    private GameObject InstantiateWithVContainer(GameObject prefab, Transform parent)
    {
        return objectResolver != null
            ? objectResolver.Instantiate(prefab, parent)
            : Instantiate(prefab, parent, false);
    }

    private void EnsureInitialized()
    {
        if (!initialized) Initialize();
    }
}
