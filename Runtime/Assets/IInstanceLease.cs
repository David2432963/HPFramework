using System;
using UnityEngine;

namespace HP.Framework.Assets
{
    /// <summary>
    /// Owns one instantiated component and the asset ownership required to keep its source alive.
    /// Dispose destroys the instance exactly once and releases the backing asset lease.
    /// </summary>
    public interface IInstanceLease<out T> : IDisposable where T : Component
    {
        T Instance { get; }
        GameObject GameObject { get; }
        bool IsValid { get; }
    }
}
