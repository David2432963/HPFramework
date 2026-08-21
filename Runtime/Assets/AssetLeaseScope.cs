using System;
using System.Collections.Generic;

namespace HP.Framework.Assets
{
    /// <summary>
    /// Explicit scope-owned lease collection. Scene/feature services can own one collection and
    /// dispose it with their VContainer scope without relying on a global registry.
    /// </summary>
    public sealed class AssetLeaseScope : IDisposable
    {
        private readonly List<IDisposable> leases = new List<IDisposable>(8);
        private bool disposed;

        public T Track<T>(T lease) where T : IDisposable
        {
            if (lease == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            if (disposed)
            {
                lease.Dispose();
                throw new ObjectDisposedException(nameof(AssetLeaseScope));
            }

            leases.Add(lease);
            return lease;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            for (int i = leases.Count - 1; i >= 0; i--)
            {
                leases[i]?.Dispose();
            }

            leases.Clear();
        }
    }
}
