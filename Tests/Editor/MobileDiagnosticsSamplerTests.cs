using System;
using HP.Framework.Assets;
using HP.Framework.Audio;
using HP.Framework.Diagnostics;
using HP.Framework.Input;
using HP.Framework.Performance;
using HP.Framework.Pooling;
using NUnit.Framework;

namespace HP.Framework.Tests.Editor
{
    public sealed class MobileDiagnosticsSamplerTests
    {
        private sealed class FakePlatformMetrics : IDiagnosticsPlatformMetrics
        {
            public long ManagedMemoryBytes { get; set; } = 1024;
            public bool GcAvailable { get; set; }
            public long GcBytes { get; set; }
            public bool TimingAvailable { get; set; }
            public int SampleCount { get; private set; }
            public bool IsDisposed { get; private set; }

            public bool TryGetGcAllocatedBytes(out long bytes)
            {
                SampleCount++;
                bytes = GcBytes;
                return GcAvailable;
            }

            public bool TryGetFrameTimings(out double cpuMilliseconds, out double gpuMilliseconds)
            {
                cpuMilliseconds = 4d;
                gpuMilliseconds = 5d;
                return TimingAvailable;
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        private sealed class FakePerformance : IPerformanceService
        {
            public PerformanceTier CurrentTier => PerformanceTier.High;
            public PerformanceProfileSO CurrentProfile => null;
            public int TargetFrameRate => 60;
            public bool HasAppliedProfile => true;
            public event Action<PerformanceTier> TierChanged;
            public void ApplyTier(PerformanceTier tier) => TierChanged?.Invoke(tier);
            public void Reapply()
            {
            }
        }

        private sealed class FakeAssets : IAssetDiagnostics
        {
            public AssetProviderStats GetStats() => new AssetProviderStats(3, 1, 4, 0, 2);
        }

        private sealed class FakePools : IPoolDiagnostics
        {
            public PoolServiceStats Stats => new PoolServiceStats(2, 5, 7, 9, 1);
            public bool TryGetStats(UnityEngine.GameObject prefab, out PoolStats stats)
            {
                stats = default;
                return false;
            }
        }

        private sealed class FakeAudio : IAudioDiagnostics
        {
            public AudioServiceStats Stats => new AudioServiceStats(3, 12, 10, 2, 1);
        }

        private sealed class FakeInput : IInputDiagnostics
        {
            public InputServiceStats Stats => new InputServiceStats("UI", 1, 2, 0);
        }

        [Test]
        public void Sampling_IsThrottledAndUsesAccumulatedFrames()
        {
            var platform = new FakePlatformMetrics();
            using var sampler = new MobileDiagnosticsSampler(
                null,
                null,
                null,
                null,
                null,
                platform,
                0.5f);

            Assert.That(sampler.TrySample(0d, 0.1f, out _), Is.False);
            Assert.That(sampler.TrySample(0.25d, 0.1f, out _), Is.False);
            Assert.That(sampler.TrySample(0.5d, 0.1f, out MobileDiagnosticsSnapshot snapshot), Is.True);
            Assert.That(snapshot.AverageFps, Is.EqualTo(10f).Within(0.001f));
            Assert.That(platform.SampleCount, Is.EqualTo(1));
            Assert.That(sampler.TrySample(0.75d, 0.1f, out _), Is.False);
        }

        [Test]
        public void UnavailableMetrics_ReturnFallbackSnapshotWithoutException()
        {
            var platform = new FakePlatformMetrics();
            using var sampler = new MobileDiagnosticsSampler(
                null,
                null,
                null,
                null,
                null,
                platform,
                0.05f);

            Assert.That(sampler.TrySample(0.051d, 0.05f, out MobileDiagnosticsSnapshot snapshot), Is.True);
            Assert.That(snapshot.GcAllocatedAvailable, Is.False);
            Assert.That(snapshot.FrameTimingAvailable, Is.False);
            Assert.That(snapshot.AssetStatsAvailable, Is.False);
            Assert.That(snapshot.PoolStatsAvailable, Is.False);
            Assert.That(snapshot.AudioStatsAvailable, Is.False);
            Assert.That(snapshot.InputStatsAvailable, Is.False);
        }

        [Test]
        public void Snapshot_UsesConsistentFrameworkStats()
        {
            var platform = new FakePlatformMetrics
            {
                GcAvailable = true,
                GcBytes = 2048,
                TimingAvailable = true
            };
            using var sampler = new MobileDiagnosticsSampler(
                new FakePerformance(),
                new FakeAssets(),
                new FakePools(),
                new FakeAudio(),
                new FakeInput(),
                platform,
                0.05f);

            Assert.That(sampler.TrySample(0.051d, 0.05f, out MobileDiagnosticsSnapshot snapshot), Is.True);
            Assert.That(snapshot.TargetFrameRate, Is.EqualTo(60));
            Assert.That(snapshot.PerformanceTier, Is.EqualTo(PerformanceTier.High));
            Assert.That(snapshot.AssetStats.LoadedAssetCount, Is.EqualTo(3));
            Assert.That(snapshot.PoolStats.ActiveInstanceCount, Is.EqualTo(5));
            Assert.That(snapshot.AudioStats.ActiveSfxVoices, Is.EqualTo(3));
            Assert.That(snapshot.InputStats.ActiveLeaseCount, Is.EqualTo(2));
            Assert.That(snapshot.GcAllocatedBytes, Is.EqualTo(2048));
            Assert.That(snapshot.CpuFrameMilliseconds, Is.EqualTo(4d));
        }

        [Test]
        public void Dispose_ReleasesPlatformMetricResources()
        {
            var platform = new FakePlatformMetrics();
            var sampler = new MobileDiagnosticsSampler(
                null,
                null,
                null,
                null,
                null,
                platform);

            sampler.Dispose();

            Assert.That(platform.IsDisposed, Is.True);
        }
    }
}
