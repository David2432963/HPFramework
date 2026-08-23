using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

using System.Threading;
using Cysharp.Threading.Tasks;

namespace HP.Framework.Pooling
{
    /// <summary>
    /// Pool owned by the LifetimeScope that resolves it. Instances are created through that
    /// scope's resolver, so scene/feature dependencies are injected correctly and the pool is
    /// disposed together with the scope.
    /// </summary>
    public sealed class ScopedPoolService : IPoolService, IPoolDiagnostics, IDisposable
    {
        private const int DefaultMaxInactiveInstancesPerPool = 64;

        private readonly GameObject rootObject;
        private readonly PoolRuntime runtime;
        private bool disposed;

        public ScopedPoolService(IObjectResolver resolver, LifetimeScope lifetimeScope)
        {
            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }

            if (lifetimeScope == null)
            {
                throw new ArgumentNullException(nameof(lifetimeScope));
            }

            rootObject = new GameObject("ScopedPoolRoot");
            rootObject.transform.SetParent(lifetimeScope.transform, false);
            runtime = new PoolRuntime(
                rootObject.transform,
                DefaultMaxInactiveInstancesPerPool,
                (prefab, parent) => resolver.Instantiate(prefab, parent));
        }

        public PoolServiceStats Stats
        {
            get
            {
                ThrowIfDisposed();
                return runtime.GetStats();
            }
        }

        public GameObject Get(GameObject prefab, Transform parent = null)
        {
            ThrowIfDisposed();
            return runtime.Get(prefab, parent);
        }

        public T Get<T>(T prefab, Transform parent = null) where T : Component
        {
            ThrowIfDisposed();
            return runtime.Get(prefab, parent);
        }

        public void Release(GameObject instance)
        {
            ThrowIfDisposed();
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
            ThrowIfDisposed();
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
            ThrowIfDisposed();
            return runtime.PrewarmAsync(prefab, count, options, cancellationToken);
        }

        public void SetMaxInactive(GameObject prefab, int maxInactiveCount)
        {
            ThrowIfDisposed();
            runtime.SetMaxInactive(prefab, maxInactiveCount);
        }

        public void Trim(GameObject prefab, int targetInactiveCount)
        {
            ThrowIfDisposed();
            runtime.Trim(prefab, targetInactiveCount);
        }

        public void TrimAll(PoolTrimPolicy policy)
        {
            ThrowIfDisposed();
            runtime.TrimAll(policy);
        }

        public bool TryGetStats(GameObject prefab, out PoolStats stats)
        {
            ThrowIfDisposed();
            return runtime.TryGetStats(prefab, out stats);
        }

        public void ClearAllPools()
        {
            ThrowIfDisposed();
            runtime.ClearAllPools();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            runtime.Dispose();
            disposed = true;
            if (rootObject != null)
            {
                PoolRuntime.DestroyOwnedObject(rootObject);
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(ScopedPoolService));
            }
        }
    }
}
