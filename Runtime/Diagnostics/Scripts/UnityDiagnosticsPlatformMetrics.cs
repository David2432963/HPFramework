using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace HP.Framework.Diagnostics
{
    public sealed class UnityDiagnosticsPlatformMetrics : IDiagnosticsPlatformMetrics
    {
        private readonly FrameTiming[] frameTimings = new FrameTiming[1];
        private ProfilerRecorder gcAllocatedRecorder;

        public UnityDiagnosticsPlatformMetrics()
        {
            try
            {
                gcAllocatedRecorder = ProfilerRecorder.StartNew(
                    ProfilerCategory.Memory,
                    "GC Allocated In Frame");
            }
            catch
            {
                gcAllocatedRecorder = default;
            }
        }

        public long ManagedMemoryBytes => Profiler.GetMonoUsedSizeLong();

        public bool TryGetGcAllocatedBytes(out long bytes)
        {
            if (gcAllocatedRecorder.Valid && gcAllocatedRecorder.IsRunning)
            {
                bytes = gcAllocatedRecorder.LastValue;
                return true;
            }

            bytes = 0;
            return false;
        }

        public bool TryGetFrameTimings(
            out double cpuMilliseconds,
            out double gpuMilliseconds)
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, frameTimings) > 0)
            {
                cpuMilliseconds = frameTimings[0].cpuFrameTime;
                gpuMilliseconds = frameTimings[0].gpuFrameTime;
                return cpuMilliseconds > 0d || gpuMilliseconds > 0d;
            }

            cpuMilliseconds = 0d;
            gpuMilliseconds = 0d;
            return false;
        }

        public void Dispose()
        {
            gcAllocatedRecorder.Dispose();
        }
    }
}
