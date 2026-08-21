using System;
using HP.Framework.Assets;
using HP.Framework.Bootstrap;
using HP.Framework.Lifecycle;
using NUnit.Framework;

namespace HP.Framework.Tests
{
    public sealed class AssetMemoryLifecycleTrimmerTests
    {
        private sealed class FakeLifecycle : IApplicationLifecycle
        {
            public bool IsPaused { get; private set; }
            public bool HasFocus { get; private set; } = true;

            public event Action Paused;
            public event Action Resumed;
            public event Action<bool> FocusChanged;
            public event Action LowMemory;
            public event Action Quitting;

            public void RaiseLowMemory() => LowMemory?.Invoke();

            // Keep the full interface shape explicit so this fake remains a useful contract guard.
            public void RaisePaused()
            {
                IsPaused = true;
                Paused?.Invoke();
            }

            public void RaiseResumed()
            {
                IsPaused = false;
                Resumed?.Invoke();
            }

            public void RaiseFocus(bool value)
            {
                HasFocus = value;
                FocusChanged?.Invoke(value);
            }

            public void RaiseQuitting() => Quitting?.Invoke();
        }

        private sealed class FakeAssetMemoryService : IAssetMemoryService
        {
            public int LoadedAssetCount { get; set; }
            public int UnusedAssetCount { get; set; }
            public int TrimCount { get; private set; }

            public void TrimUnused()
            {
                TrimCount++;
            }
        }

        [Test]
        public void LowMemory_TriggersTrimOncePerSignal_AndDisposeUnsubscribes()
        {
            var lifecycle = new FakeLifecycle();
            var memory = new FakeAssetMemoryService();
            var trimmer = new AssetMemoryLifecycleTrimmer(lifecycle, memory);

            trimmer.Initialize();
            trimmer.Initialize();
            lifecycle.RaiseLowMemory();
            Assert.That(memory.TrimCount, Is.EqualTo(1));

            trimmer.Dispose();
            lifecycle.RaiseLowMemory();
            Assert.That(memory.TrimCount, Is.EqualTo(1));
        }
    }
}
