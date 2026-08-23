using HP.Framework.Assets;
using HP.Framework.Audio;
using HP.Framework.Input;
using HP.Framework.Performance;
using HP.Framework.Pooling;

namespace HP.Framework.Diagnostics
{
    public readonly struct MobileDiagnosticsSnapshot
    {
        public MobileDiagnosticsSnapshot(
            float averageFps,
            float frameTimeMilliseconds,
            int targetFrameRate,
            PerformanceTier performanceTier,
            long managedMemoryBytes,
            bool gcAllocatedAvailable,
            long gcAllocatedBytes,
            bool frameTimingAvailable,
            double cpuFrameMilliseconds,
            double gpuFrameMilliseconds,
            bool assetStatsAvailable,
            AssetProviderStats assetStats,
            bool poolStatsAvailable,
            PoolServiceStats poolStats,
            bool audioStatsAvailable,
            AudioServiceStats audioStats,
            bool inputStatsAvailable,
            InputServiceStats inputStats)
        {
            AverageFps = averageFps;
            FrameTimeMilliseconds = frameTimeMilliseconds;
            TargetFrameRate = targetFrameRate;
            PerformanceTier = performanceTier;
            ManagedMemoryBytes = managedMemoryBytes;
            GcAllocatedAvailable = gcAllocatedAvailable;
            GcAllocatedBytes = gcAllocatedBytes;
            FrameTimingAvailable = frameTimingAvailable;
            CpuFrameMilliseconds = cpuFrameMilliseconds;
            GpuFrameMilliseconds = gpuFrameMilliseconds;
            AssetStatsAvailable = assetStatsAvailable;
            AssetStats = assetStats;
            PoolStatsAvailable = poolStatsAvailable;
            PoolStats = poolStats;
            AudioStatsAvailable = audioStatsAvailable;
            AudioStats = audioStats;
            InputStatsAvailable = inputStatsAvailable;
            InputStats = inputStats;
        }

        public float AverageFps { get; }
        public float FrameTimeMilliseconds { get; }
        public int TargetFrameRate { get; }
        public PerformanceTier PerformanceTier { get; }
        public long ManagedMemoryBytes { get; }
        public bool GcAllocatedAvailable { get; }
        public long GcAllocatedBytes { get; }
        public bool FrameTimingAvailable { get; }
        public double CpuFrameMilliseconds { get; }
        public double GpuFrameMilliseconds { get; }
        public bool AssetStatsAvailable { get; }
        public AssetProviderStats AssetStats { get; }
        public bool PoolStatsAvailable { get; }
        public PoolServiceStats PoolStats { get; }
        public bool AudioStatsAvailable { get; }
        public AudioServiceStats AudioStats { get; }
        public bool InputStatsAvailable { get; }
        public InputServiceStats InputStats { get; }
    }
}
