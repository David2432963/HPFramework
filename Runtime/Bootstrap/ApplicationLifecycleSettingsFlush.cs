using System;
using HP.Framework.Common;
using HP.Framework.Lifecycle;
using HP.Framework.Persistence;
using VContainer.Unity;

namespace HP.Framework.Bootstrap
{
    /// <summary>
    /// Composition adapter that flushes dirty settings at mobile-safe lifecycle boundaries.
    /// Lifecycle remains independent from Persistence; the dependency is owned by Bootstrap.
    /// </summary>
    internal sealed class ApplicationLifecycleSettingsFlush : IInitializable, IDisposable
    {
        private readonly IApplicationLifecycle lifecycle;
        private readonly ISettingsFlushService settings;
        private bool initialized;

        public ApplicationLifecycleSettingsFlush(
            IApplicationLifecycle lifecycle,
            ISettingsFlushService settings)
        {
            this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            lifecycle.Paused += FlushIfDirty;
            lifecycle.Quitting += FlushIfDirty;
            initialized = true;
        }

        public void Dispose()
        {
            if (!initialized)
            {
                return;
            }

            lifecycle.Paused -= FlushIfDirty;
            lifecycle.Quitting -= FlushIfDirty;
            initialized = false;
        }

        private void FlushIfDirty()
        {
            try
            {
                settings.SaveIfDirty();
            }
            catch (Exception exception)
            {
                BaseLog.LogException(exception);
            }
        }
    }
}
