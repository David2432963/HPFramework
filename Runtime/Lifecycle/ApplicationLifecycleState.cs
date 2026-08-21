namespace HP.Framework.Lifecycle
{
    internal struct ApplicationLifecycleState
    {
        private bool initialized;

        internal bool IsPaused { get; private set; }
        internal bool HasFocus { get; private set; }
        internal bool IsQuitting { get; private set; }

        internal void Initialize(bool hasFocus)
        {
            if (initialized)
            {
                return;
            }

            HasFocus = hasFocus;
            initialized = true;
        }

        internal bool TrySetPaused(bool paused)
        {
            if (IsPaused == paused)
            {
                return false;
            }

            IsPaused = paused;
            return true;
        }

        internal bool TrySetFocus(bool hasFocus)
        {
            if (HasFocus == hasFocus)
            {
                return false;
            }

            HasFocus = hasFocus;
            return true;
        }

        internal bool TryBeginQuit()
        {
            if (IsQuitting)
            {
                return false;
            }

            IsQuitting = true;
            return true;
        }
    }
}
