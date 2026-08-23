using System;
using HP.Framework.Performance;
using HP.Framework.Persistence;
using VContainer.Unity;

namespace HP.Framework.Bootstrap
{
    public sealed class PerformancePreferenceCoordinator : IPerformancePreferenceService, IInitializable
    {
        private readonly ISettingsService settings;
        private readonly IPerformanceService performance;
        private readonly IDevicePerformanceClassifier classifier;
        private bool initialized;

        public PerformancePreferenceCoordinator(
            ISettingsService settings,
            IPerformanceService performance,
            IDevicePerformanceClassifier classifier)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.performance = performance ?? throw new ArgumentNullException(nameof(performance));
            this.classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
        }

        public bool UseAutomaticTier => settings.UseAutomaticPerformanceTier;

        public PerformanceTier PreferredTier => Enum.IsDefined(
                typeof(PerformanceTier),
                settings.PreferredPerformanceTier)
            ? (PerformanceTier)settings.PreferredPerformanceTier
            : PerformanceTier.Medium;

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            ApplyPreference();
        }

        public void UseAutomatic()
        {
            settings.UseAutomaticPerformanceTier = true;
            performance.ApplyTier(classifier.RecommendTier());
        }

        public void SelectTier(PerformanceTier tier)
        {
            if (!Enum.IsDefined(typeof(PerformanceTier), tier))
            {
                throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown performance tier.");
            }

            settings.PreferredPerformanceTier = (int)tier;
            settings.UseAutomaticPerformanceTier = false;
            performance.ApplyTier(tier);
        }

        private void ApplyPreference()
        {
            if (settings.UseAutomaticPerformanceTier)
            {
                performance.ApplyTier(classifier.RecommendTier());
                return;
            }

            performance.ApplyTier(PreferredTier);
        }
    }
}
