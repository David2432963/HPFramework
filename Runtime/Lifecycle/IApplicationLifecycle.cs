using System;

namespace HP.Framework.Lifecycle
{
    /// <summary>
    /// Application-owned lifecycle signal source. Consumers receive normalized, idempotent
    /// lifecycle events instead of implementing Unity application callbacks independently.
    /// </summary>
    public interface IApplicationLifecycle
    {
        bool IsPaused { get; }
        bool HasFocus { get; }

        event Action Paused;
        event Action Resumed;
        event Action<bool> FocusChanged;
        event Action LowMemory;
        event Action Quitting;
    }
}
