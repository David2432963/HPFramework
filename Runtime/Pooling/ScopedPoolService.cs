using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace HP.Framework.Pooling
{
    /// <summary>
    /// Pool owned by the LifetimeScope that resolves it. Instances are created through that
    /// scope's resolver, so scene/feature dependencies are injected correctly and the pool is
    /// disposed together with the scope.
    /// </summary>
    public sealed class ScopedPoolService : IPoolService, IDisposable
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
                UnityEngine.Object.Destroy(rootObject);
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


