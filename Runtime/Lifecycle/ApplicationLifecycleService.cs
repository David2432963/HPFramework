using System;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("HP.Framework.Tests.Editor")]
[assembly: InternalsVisibleTo("HP.Framework.Tests.Runtime")]

namespace HP.Framework.Lifecycle
{
    /// <summary>
    /// Root-owned Unity lifecycle bridge. It normalizes Unity callbacks into idempotent framework
    /// signals and performs no per-frame work.
    /// </summary>
    public sealed class ApplicationLifecycleService : MonoBehaviour, IApplicationLifecycle, IDisposable
    {
        private ApplicationLifecycleState state;
        private bool lowMemorySubscribed;
        private bool disposed;

        public bool IsPaused => state.IsPaused;
        public bool HasFocus => state.HasFocus;

        public event Action Paused;
        public event Action Resumed;
        public event Action<bool> FocusChanged;
        public event Action LowMemory;
        public event Action Quitting;

        internal bool IsLowMemorySubscribed => lowMemorySubscribed;
        internal bool IsQuitting => state.IsQuitting;

        private void Awake()
        {
            state.Initialize(Application.isFocused);
        }

        private void OnEnable()
        {
            state.Initialize(Application.isFocused);
            SubscribeLowMemory();
        }

        private void OnDisable()
        {
            UnsubscribeLowMemory();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            SetPaused(pauseStatus);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            SetFocus(hasFocus);
        }

        private void OnApplicationQuit()
        {
            SignalQuitting();
        }

        internal bool SetPaused(bool paused)
        {
            if (disposed || !state.TrySetPaused(paused))
            {
                return false;
            }

            if (paused)
            {
                Paused?.Invoke();
            }
            else
            {
                Resumed?.Invoke();
            }

            return true;
        }

        internal bool SetFocus(bool hasFocus)
        {
            if (disposed || !state.TrySetFocus(hasFocus))
            {
                return false;
            }

            FocusChanged?.Invoke(hasFocus);
            return true;
        }

        internal void SignalLowMemory()
        {
            if (!disposed)
            {
                LowMemory?.Invoke();
            }
        }

        internal bool SignalQuitting()
        {
            if (disposed || !state.TryBeginQuit())
            {
                return false;
            }

            Quitting?.Invoke();
            return true;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            UnsubscribeLowMemory();
            Paused = null;
            Resumed = null;
            FocusChanged = null;
            LowMemory = null;
            Quitting = null;
        }

        private void SubscribeLowMemory()
        {
            if (disposed || lowMemorySubscribed)
            {
                return;
            }

            Application.lowMemory += SignalLowMemory;
            lowMemorySubscribed = true;
        }

        private void UnsubscribeLowMemory()
        {
            if (!lowMemorySubscribed)
            {
                return;
            }

            Application.lowMemory -= SignalLowMemory;
            lowMemorySubscribed = false;
        }
    }
}
