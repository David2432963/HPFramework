using System;
using System.Collections.Generic;
using UnityEngine;

namespace HP.Framework.Performance
{
    public sealed class PerformanceService : IPerformanceService, IPerformanceDiagnosticsControl
    {
        private readonly PerformanceCatalogSO catalog;
        private readonly List<IPerformanceRendererApplier> rendererAppliers;
        private readonly List<IPerformanceTierApplier> tierAppliers;

        public PerformanceService(
            PerformanceCatalogSO catalog,
            IEnumerable<IPerformanceRendererApplier> rendererAppliers,
            IEnumerable<IPerformanceTierApplier> tierAppliers)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.rendererAppliers = Sort(rendererAppliers);
            this.tierAppliers = Sort(tierAppliers);
            CurrentTier = catalog.DefaultTier;
        }

        public PerformanceTier CurrentTier { get; private set; }
        public PerformanceProfileSO CurrentProfile { get; private set; }
        public int TargetFrameRate { get; private set; }
        public bool HasAppliedProfile { get; private set; }
        public bool HasFrameRateOverride { get; private set; }
        public int EffectiveTargetFrameRate => TargetFrameRate;

        public event Action<PerformanceTier> TierChanged;

        public void ApplyTier(PerformanceTier tier)
        {
            PerformanceProfileSO profile = ResolveProfile(tier);
            if (HasAppliedProfile && ReferenceEquals(CurrentProfile, profile) && CurrentTier == profile.Tier)
            {
                return;
            }

            ApplyProfile(profile, publishTierChanged: true);
        }

        public void Reapply()
        {
            PerformanceProfileSO profile = CurrentProfile != null
                ? CurrentProfile
                : ResolveProfile(CurrentTier);
            ApplyProfile(profile, publishTierChanged: false);
        }

        public void SetFrameRateOverride(int targetFrameRate)
        {
            if (targetFrameRate <= 0)
            {
                ClearFrameRateOverride();
                return;
            }

            HasFrameRateOverride = true;
            int normalized = FrameRatePolicy.NormalizeTarget(
                targetFrameRate,
                Screen.currentResolution.refreshRateRatio.value,
                allowUnlimited: false);
            Application.targetFrameRate = normalized;
            TargetFrameRate = normalized;
        }

        public void ClearFrameRateOverride()
        {
            if (!HasFrameRateOverride)
            {
                return;
            }

            HasFrameRateOverride = false;
            PerformanceProfileSO profile = CurrentProfile != null
                ? CurrentProfile
                : ResolveProfile(CurrentTier);
            int normalized = FrameRatePolicy.NormalizeTarget(
                profile.TargetFrameRate,
                Screen.currentResolution.refreshRateRatio.value,
                profile.AllowUnlimitedFrameRate);
            Application.targetFrameRate = normalized;
            TargetFrameRate = normalized;
        }

        private PerformanceProfileSO ResolveProfile(PerformanceTier requestedTier)
        {
            if (!catalog.TryResolveProfile(requestedTier, out PerformanceProfileSO profile) || profile == null)
            {
                throw new InvalidOperationException(
                    $"Performance catalog '{catalog.name}' has no usable profiles.");
            }

            return profile;
        }

        private void ApplyProfile(PerformanceProfileSO profile, bool publishTierChanged)
        {
            int previousTierValue = (int)CurrentTier;
            int maxQualityLevel = Mathf.Max(0, QualitySettings.names.Length - 1);
            int qualityLevel = Mathf.Clamp(profile.UnityQualityLevel, 0, maxQualityLevel);

            // 1. profile was resolved before entering this method.
            // 2. Apply Unity quality first because it may overwrite explicit overrides.
            QualitySettings.SetQualityLevel(qualityLevel, true);

            // 3. Framework-owned explicit overrides.
            double refreshRate = Screen.currentResolution.refreshRateRatio.value;
            int targetFrameRate = HasFrameRateOverride
                ? TargetFrameRate
                : FrameRatePolicy.NormalizeTarget(
                    profile.TargetFrameRate,
                    refreshRate,
                    profile.AllowUnlimitedFrameRate);
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.lodBias = Mathf.Max(0.01f, profile.LodBias);

            // 4. Optional renderer integrations.
            for (int i = 0; i < rendererAppliers.Count; i++)
            {
                rendererAppliers[i].Apply(profile);
            }

            // 5. Game-specific tier consumers.
            for (int i = 0; i < tierAppliers.Count; i++)
            {
                tierAppliers[i].Apply(profile);
            }

            // 6. Commit runtime state.
            CurrentProfile = profile;
            CurrentTier = profile.Tier;
            TargetFrameRate = targetFrameRate;
            HasAppliedProfile = true;

            // 7. Publish only after all runtime state is committed.
            if (publishTierChanged && previousTierValue != (int)CurrentTier)
            {
                TierChanged?.Invoke(CurrentTier);
            }
        }

        private static List<T> Sort<T>(IEnumerable<T> values)
        {
            var result = values != null ? new List<T>(values) : new List<T>();
            result.Sort((left, right) =>
            {
                int leftOrder = GetOrder(left);
                int rightOrder = GetOrder(right);
                int orderCompare = leftOrder.CompareTo(rightOrder);
                if (orderCompare != 0)
                {
                    return orderCompare;
                }

                string leftName = left?.GetType().FullName ?? string.Empty;
                string rightName = right?.GetType().FullName ?? string.Empty;
                return string.CompareOrdinal(leftName, rightName);
            });
            return result;
        }

        private static int GetOrder<T>(T value)
        {
            if (value is IPerformanceRendererApplier renderer)
            {
                return renderer.Order;
            }

            if (value is IPerformanceTierApplier tier)
            {
                return tier.Order;
            }

            return 0;
        }
    }
}
