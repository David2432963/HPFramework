namespace HP.Framework.Pooling
{
    using System;
    using HP.Framework.Pooling;
    using UnityEngine;
    using VContainer;
    using VContainer.Unity;

    /// <summary>
    /// Application-lifetime pool. Child scopes should call builder.RegisterScopedPool() so their
    /// prefabs are instantiated through the child resolver instead of this root resolver.
    /// </summary>
    public sealed class PoolManager : MonoBehaviour, IPoolService, IInitializable, IDisposable
    {
        [SerializeField] private Transform rootPoolParent;
        [SerializeField, Min(1)] private int maxInactiveInstancesPerPool = 64;

        private IObjectResolver objectResolver;
        private PoolRuntime runtime;
        private bool initialized;

        [Inject]
        public void Construct(IObjectResolver resolver)
        {
            objectResolver = resolver;
        }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            // The manager object is the default application-pool root. This keeps the Bootstrap
            // hierarchy flat while each prefab still receives its own Pool_<Prefab> child.
            rootPoolParent ??= transform;

            runtime = new PoolRuntime(
                rootPoolParent,
                maxInactiveInstancesPerPool,
                InstantiateWithVContainer);
            initialized = true;
        }

        public GameObject Get(GameObject prefab, Transform parent = null)
        {
            EnsureInitialized();
            return runtime.Get(prefab, parent);
        }

        public T Get<T>(T prefab, Transform parent = null) where T : Component
        {
            EnsureInitialized();
            return runtime.Get(prefab, parent);
        }

        public void Release(GameObject instance)
        {
            EnsureInitialized();
            runtime.Release(instance);
        }

        public void Release<T>(T instance) where T : Component
        {
            if (instance != null)
            {
                Release(instance.gameObject);
            }
        }

        public void Prewarm(GameObject prefab, int count)
        {
            EnsureInitialized();
            runtime.Prewarm(prefab, count);
        }

        public void Prewarm<T>(T prefab, int count) where T : Component
        {
            if (prefab != null)
            {
                Prewarm(prefab.gameObject, count);
            }
        }

        public void ClearAllPools()
        {
            runtime?.ClearAllPools();
        }

        public void Dispose()
        {
            runtime?.Dispose();
            runtime = null;
            initialized = false;
        }

        private GameObject InstantiateWithVContainer(GameObject prefab, Transform parent)
        {
            return objectResolver != null
                ? objectResolver.Instantiate(prefab, parent)
                : Instantiate(prefab, parent, false);
        }

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                Initialize();
            }
        }
    }


}


