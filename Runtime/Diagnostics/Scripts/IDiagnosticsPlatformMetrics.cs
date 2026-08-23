using System;

namespace HP.Framework.Diagnostics
{
    public interface IDiagnosticsPlatformMetrics : IDisposable
    {
        long ManagedMemoryBytes { get; }
        bool TryGetGcAllocatedBytes(out long bytes);
        bool TryGetFrameTimings(out double cpuMilliseconds, out double gpuMilliseconds);
    }
}
