using HP.Framework.Haptics;
using NUnit.Framework;
using UnityEngine;

namespace HP.Framework.Tests.Editor
{
    public sealed class HapticManagerTests
    {
        private sealed class FakeBackend : IHapticBackend
        {
            public bool IsAvailable { get; set; } = true;
            public HapticType? LastType { get; private set; }
            public long LastDuration { get; private set; }
            public int PlayCount { get; private set; }
            public bool IsDisposed { get; private set; }

            public void Play(HapticType type)
            {
                LastType = type;
                PlayCount++;
            }

            public void Vibrate(long milliseconds)
            {
                LastDuration = milliseconds;
                PlayCount++;
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        [Test]
        public void SemanticPlay_ForwardsTypeToBackend()
        {
            HapticManager manager = CreateManager(out GameObject owner, out FakeBackend backend);
            try
            {
                manager.Play(HapticType.Success);

                Assert.That(backend.LastType, Is.EqualTo(HapticType.Success));
                Assert.That(backend.PlayCount, Is.EqualTo(1));
            }
            finally
            {
                manager.Dispose();
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void DisabledPreference_SuppressesSemanticAndRawHaptics()
        {
            HapticManager manager = CreateManager(out GameObject owner, out FakeBackend backend);
            try
            {
                manager.IsHapticEnabled = false;
                manager.Play(HapticType.Warning);
                manager.VibrateCustom(100);

                Assert.That(backend.PlayCount, Is.Zero);
            }
            finally
            {
                manager.Dispose();
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void CompatibilityMethods_UseExpectedDurations()
        {
            HapticManager manager = CreateManager(out GameObject owner, out FakeBackend backend);
            try
            {
                manager.VibrateShort();
                Assert.That(backend.LastDuration, Is.EqualTo(30));
                manager.VibrateLong();
                Assert.That(backend.LastDuration, Is.EqualTo(150));
            }
            finally
            {
                manager.Dispose();
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Dispose_ReleasesPlatformBackend()
        {
            HapticManager manager = CreateManager(out GameObject owner, out FakeBackend backend);

            manager.Dispose();

            Assert.That(backend.IsDisposed, Is.True);
            Object.DestroyImmediate(owner);
        }

        private static HapticManager CreateManager(
            out GameObject owner,
            out FakeBackend backend)
        {
            owner = new GameObject("HapticManager_Test");
            HapticManager manager = owner.AddComponent<HapticManager>();
            backend = new FakeBackend();
            manager.ConfigureBackend(backend);
            manager.Initialize();
            return manager;
        }
    }
}
