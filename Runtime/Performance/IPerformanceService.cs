using System;

namespace HP.Framework.Performance
{
    public interface IPerformanceService
    {
        PerformanceTier CurrentTier { get; }
        PerformanceProfileSO CurrentProfile { get; }
        int TargetFrameRate { get; }
        bool HasAppliedProfile { get; }

        event Action<PerformanceTier> TierChanged;

        void ApplyTier(PerformanceTier tier);
        void Reapply();
    }

    public interface IPerformanceDiagnosticsControl
    {
        bool HasFrameRateOverride { get; }
        int EffectiveTargetFrameRate { get; }
        void SetFrameRateOverride(int targetFrameRate);
        void ClearFrameRateOverride();
    }

    public interface IPerformancePreferenceService
    {
        bool UseAutomaticTier { get; }
        PerformanceTier PreferredTier { get; }

        void UseAutomatic();
        void SelectTier(PerformanceTier tier);
    }
}
