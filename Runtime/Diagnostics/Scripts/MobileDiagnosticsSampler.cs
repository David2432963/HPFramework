using System;
using HP.Framework.Assets;
using HP.Framework.Audio;
using HP.Framework.Input;
using HP.Framework.Performance;
using HP.Framework.Pooling;

namespace HP.Framework.Diagnostics
{
    public sealed class MobileDiagnosticsSampler : IDisposable
    {
        private readonly IPerformanceService performance;
        private readonly IAssetDiagnostics assets;
        private readonly IPoolDiagnostics pools;
        private readonly IAudioDiagnostics audio;
        private readonly IInputDiagnostics input;
        private readonly IDiagnosticsPlatformMetrics platformMetrics;
        private readonly double refreshInterval;

        private double nextRefreshTime;
        private float accumulatedTime;
        private int accumulatedFrames;

        public MobileDiagnosticsSampler(
            IPerformanceService performance,
            IAssetDiagnostics assets,
            IPoolDiagnostics pools,
            IAudioDiagnostics audio,
            IInputDiagnostics input,
            IDiagnosticsPlatformMetrics platformMetrics,
            float refreshInterval = 0.5f)
        {
            this.performance = performance;
            this.assets = assets;
            this.pools = pools;
            this.audio = audio;
            this.input = input;
            this.platformMetrics = platformMetrics
                ?? throw new ArgumentNullException(nameof(platformMetrics));
            this.refreshInterval = Math.Max(0.05d, refreshInterval);
            nextRefreshTime = this.refreshInterval;
        }

        public bool TrySample(
            double unscaledTime,
            float unscaledDeltaTime,
            out MobileDiagnosticsSnapshot snapshot)
        {
            accumulatedFrames++;
            accumulatedTime += Math.Max(0f, unscaledDeltaTime);
            if (unscaledTime < nextRefreshTime)
            {
                snapshot = default;
                return false;
            }

            float averageFps = accumulatedTime > 0f
                ? accumulatedFrames / accumulatedTime
                : 0f;
            float frameTime = averageFps > 0f ? 1000f / averageFps : 0f;
            accumulatedFrames = 0;
            accumulatedTime = 0f;
            nextRefreshTime = unscaledTime + refreshInterval;

            bool gcAvailable = platformMetrics.TryGetGcAllocatedBytes(out long gcBytes);
            bool frameTimingAvailable = platformMetrics.TryGetFrameTimings(
                out double cpuMilliseconds,
                out double gpuMilliseconds);
            bool assetAvailable = assets != null;
            bool poolAvailable = pools != null;
            bool audioAvailable = audio != null;
            bool inputAvailable = input != null && !(input is UnavailableInputDiagnostics);

            snapshot = new MobileDiagnosticsSnapshot(
                averageFps,
                frameTime,
                performance?.TargetFrameRate ?? UnityEngine.Application.targetFrameRate,
                performance?.CurrentTier ?? default,
                platformMetrics.ManagedMemoryBytes,
                gcAvailable,
                gcBytes,
                frameTimingAvailable,
                cpuMilliseconds,
                gpuMilliseconds,
                assetAvailable,
                assetAvailable ? assets.GetStats() : default,
                poolAvailable,
                poolAvailable ? pools.Stats : default,
                audioAvailable,
                audioAvailable ? audio.Stats : default,
                inputAvailable,
                inputAvailable ? input.Stats : default);
            return true;
        }

        public void Dispose()
        {
            platformMetrics.Dispose();
        }
    }
}
