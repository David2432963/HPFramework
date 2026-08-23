using HP.Framework.Performance;
using UnityEngine;
using VContainer;

namespace HP.Framework.Samples.MobilePerformance
{
    public sealed class SamplePerformanceMenu : MonoBehaviour
    {
        private IPerformancePreferenceService preferences;

        [Inject]
        public void Construct(IPerformancePreferenceService preferences)
        {
            this.preferences = preferences;
        }

        public void SelectBatterySaver() => preferences.SelectTier(PerformanceTier.Low);
        public void SelectBalanced() => preferences.SelectTier(PerformanceTier.Medium);
        public void SelectHighQuality() => preferences.SelectTier(PerformanceTier.High);
        public void SelectAutomatic() => preferences.UseAutomatic();
    }
}
