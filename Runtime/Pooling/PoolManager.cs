namespace HP.Framework.Pooling
{
    using System;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UnityEngine;
    using VContainer;
    using VContainer.Unity;

    /// <summary>
    /// Application-lifetime pool. Child scopes should call builder.RegisterScopedPool() so their
    /// prefabs are instantiated through the child resolver instead of this root resolver.
    /// </summary>
    public sealed class PoolManager : MonoBehaviour, IPoolService, IPoolDiagnostics, IInitializable, IDisposable
    {
        [SerializeField] private Transform rootPoolParent;
        [SerializeField, Min(1)] private int maxInactiveInstancesPerPool = 64;
        [SerializeField] private PoolConfigSO poolConfig;

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
            ApplyConfiguredCapacities();
            initialized = true;
        }

        public PoolServiceStats Stats
        {
            get
            {
                EnsureInitialized();
                return runtime.GetStats();
            }
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

        public UniTask PrewarmAsync(
            GameObject prefab,
            int count,
            PoolPrewarmOptions options,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            return runtime.PrewarmAsync(prefab, count, options, cancellationToken);
        }

        public async UniTask PrewarmConfiguredPoolsAsync(
            PoolPrewarmOptions options,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            if (poolConfig == null)
            {
                return;
            }

            foreach (PoolDefinition definition in poolConfig.Definitions)
            {
                if (definition?.Prefab == null || definition.PrewarmCount <= 0)
                {
                    continue;
                }

                await runtime.PrewarmAsync(
                    definition.Prefab,
                    definition.PrewarmCount,
                    options,
                    cancellationToken);
            }
        }

        public void SetMaxInactive(GameObject prefab, int maxInactiveCount)
        {
            EnsureInitialized();
            runtime.SetMaxInactive(prefab, maxInactiveCount);
        }

        public void Trim(GameObject prefab, int targetInactiveCount)
        {
            EnsureInitialized();
            runtime.Trim(prefab, targetInactiveCount);
        }

        public void TrimAll(PoolTrimPolicy policy)
        {
            EnsureInitialized();
            runtime.TrimAll(policy);
        }

        public bool TryGetStats(GameObject prefab, out PoolStats stats)
        {
            EnsureInitialized();
            return runtime.TryGetStats(prefab, out stats);
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

        private void ApplyConfiguredCapacities()
        {
            if (poolConfig == null)
            {
                return;
            }

            foreach (PoolDefinition definition in poolConfig.Definitions)
            {
                if (definition?.Prefab != null)
                {
                    runtime.SetMaxInactive(definition.Prefab, definition.MaxInactiveCount);
                }
            }
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

