using System;
using UnityEngine;

namespace HP.Framework.Assets
{
    /// <summary>
    /// Explicit ownership handle for an asset acquired through an IAssetLeaseProvider.
    /// Disposing the lease releases exactly one logical reference.
    /// </summary>
    public interface IAssetLease<out T> : IDisposable where T : UnityEngine.Object
    {
        string Key { get; }
        T Asset { get; }
        bool IsValid { get; }
    }
}
