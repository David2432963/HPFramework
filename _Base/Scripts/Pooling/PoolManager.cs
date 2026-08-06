using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Base.Pooling;

/// <summary>
/// Global manager for caching and reusing GameObjects/Components.
/// Implements IPoolService.
/// Integrated with VContainer native atomic instantiation (Instantiate + Inject in 1 step).
/// </summary>
public sealed class PoolManager : MonoBehaviour, IPoolService
{
    private class GameObjectPool
    {
        private readonly GameObject prefab;
        private readonly Transform poolParent;
        private readonly Stack<GameObject> inactiveInstances = new Stack<GameObject>();
        private readonly Func<GameObject, Transform, GameObject> instantiator;

        public GameObjectPool(GameObject prefab, Transform poolParent, Func<GameObject, Transform, GameObject> instantiator)
        {
            this.prefab = prefab;
            this.poolParent = poolParent;
            this.instantiator = instantiator;
        }

        public GameObject Get(Transform parent)
        {
            while (inactiveInstances.Count > 0)
            {
                GameObject instance = inactiveInstances.Pop();
                if (instance != null)
                {
                    instance.transform.SetParent(parent, false);
                    instance.SetActive(true);
                    return instance;
                }
            }

            return CreateInstance(parent);
        }

        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject instance = CreateInstance(poolParent);
                instance.SetActive(false);
                inactiveInstances.Push(instance);
            }
        }

        public void Release(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            instance.SetActive(false);
            instance.transform.SetParent(poolParent, false);
            inactiveInstances.Push(instance);
        }

        public void Clear()
        {
            while (inactiveInstances.Count > 0)
            {
                var instance = inactiveInstances.Pop();
                if (instance != null)
                {
                    UnityEngine.Object.Destroy(instance);
                }
            }

            if (poolParent != null)
            {
                UnityEngine.Object.Destroy(poolParent.gameObject);
            }
        }

        private GameObject CreateInstance(Transform parent)
        {
            return instantiator(prefab, parent);
        }
    }

    private readonly Dictionary<GameObject, GameObjectPool> pools = new Dictionary<GameObject, GameObjectPool>();
    private readonly Dictionary<GameObject, GameObjectPool> instanceToPoolMap = new Dictionary<GameObject, GameObjectPool>();
    [SerializeField] private Transform rootPoolParent;

    private IObjectResolver objectResolver;

    [Inject]
    public void Construct(IObjectResolver resolver)
    {
        objectResolver = resolver;
    }

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        ClearAllPools();
    }

    public GameObject Get(GameObject prefab, Transform parent = null)
    {
        if (prefab == null)
        {
            return null;
        }

        var pool = GetOrCreatePool(prefab);
        var instance = pool.Get(parent);
        instanceToPoolMap[instance] = pool;
        return instance;
    }

    public T Get<T>(T prefab, Transform parent = null) where T : Component
    {
        if (prefab == null)
        {
            return null;
        }

        var goInstance = Get(prefab.gameObject, parent);
        return goInstance != null ? goInstance.GetComponent<T>() : null;
    }

    public void Release(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        if (instanceToPoolMap.TryGetValue(instance, out var pool))
        {
            instanceToPoolMap.Remove(instance);
            pool.Release(instance);
        }
        else
        {
            UnityEngine.Object.Destroy(instance);
        }
    }

    public void Release<T>(T instance) where T : Component
    {
        if (instance == null)
        {
            return;
        }
        Release(instance.gameObject);
    }

    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0)
        {
            return;
        }

        var pool = GetOrCreatePool(prefab);
        pool.Prewarm(count);
    }

    public void Prewarm<T>(T prefab, int count) where T : Component
    {
        if (prefab == null)
        {
            return;
        }
        Prewarm(prefab.gameObject, count);
    }

    private GameObject InstantiateWithVContainer(GameObject prefab, Transform parent)
    {
        if (objectResolver != null)
        {
            return objectResolver.Instantiate(prefab, parent);
        }
        return Instantiate(prefab, parent, false);
    }

    private GameObjectPool GetOrCreatePool(GameObject prefab)
    {
        if (!pools.TryGetValue(prefab, out var pool))
        {
            var poolParentGo = new GameObject($"Pool_{prefab.name}");
            poolParentGo.transform.SetParent(rootPoolParent != null ? rootPoolParent : transform);
            pool = new GameObjectPool(prefab, poolParentGo.transform, InstantiateWithVContainer);
            pools[prefab] = pool;
        }
        return pool;
    }

    public void ClearAllPools()
    {
        foreach (var pool in pools.Values)
        {
            pool.Clear();
        }
        pools.Clear();
        instanceToPoolMap.Clear();
    }
}
