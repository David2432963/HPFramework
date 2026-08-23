using System;
using System.Collections.Generic;
using HP.Framework.Bootstrap;
using HP.Framework.Performance;
using HP.Framework.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace HP.Framework.Tests
{
    public sealed class PerformanceServiceTests
    {
        private int originalTargetFrameRate;
        private int originalQualityLevel;
        private float originalLodBias;

        [SetUp]
        public void SetUp()
        {
            originalTargetFrameRate = Application.targetFrameRate;
            originalQualityLevel = QualitySettings.GetQualityLevel();
            originalLodBias = QualitySettings.lodBias;
        }

        [TearDown]
        public void TearDown()
        {
            Application.targetFrameRate = originalTargetFrameRate;
            QualitySettings.SetQualityLevel(originalQualityLevel, true);
            QualitySettings.lodBias = originalLodBias;
        }

        [Test]
        public void FrameRatePolicy_NormalizesToDisplayFriendlyDivisor()
        {
            Assert.That(FrameRatePolicy.NormalizeTarget(60, 120, false), Is.EqualTo(60));
            Assert.That(FrameRatePolicy.NormalizeTarget(40, 120, false), Is.EqualTo(40));
            Assert.That(FrameRatePolicy.NormalizeTarget(45, 90, false), Is.EqualTo(45));
            Assert.That(FrameRatePolicy.NormalizeTarget(120, 60, false), Is.EqualTo(60));
            Assert.That(FrameRatePolicy.NormalizeTarget(-1, 120, true), Is.EqualTo(-1));
        }

        [Test]
        public void ApplyTier_UsesFallbackProfile_ClampsQuality_AndIsIdempotent()
        {
            PerformanceProfileSO low = CreateProfile(PerformanceTier.Low, 30, int.MaxValue, 0.7f);
            PerformanceCatalogSO catalog = CreateCatalog(PerformanceTier.Medium, low);
            var applier = new TrackingTierApplier(0, "tier");
            var service = new PerformanceService(catalog, Array.Empty<IPerformanceRendererApplier>(), new[] { applier });
            try
            {
                service.ApplyTier(PerformanceTier.High);
                Assert.That(service.CurrentProfile, Is.SameAs(low));
                Assert.That(service.CurrentTier, Is.EqualTo(PerformanceTier.Low));
                Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(Mathf.Max(0, QualitySettings.names.Length - 1)));
                Assert.That(QualitySettings.lodBias, Is.EqualTo(0.7f).Within(0.0001f));
                Assert.That(applier.Count, Is.EqualTo(1));

                service.ApplyTier(PerformanceTier.High);
                Assert.That(applier.Count, Is.EqualTo(1));
                service.Reapply();
                Assert.That(applier.Count, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(low);
            }
        }

        [Test]
        public void ApplyTier_UsesDeterministicOrdering_AndPublishesAfterCommit()
        {
            PerformanceProfileSO high = CreateProfile(PerformanceTier.High, 60, 0, 1.25f);
            PerformanceCatalogSO catalog = CreateCatalog(PerformanceTier.Medium, high);
            var calls = new List<string>();
            var service = new PerformanceService(
                catalog,
                new IPerformanceRendererApplier[]
                {
                    new TrackingRendererApplier(10, "R10", calls),
                    new TrackingRendererApplier(0, "R0", calls)
                },
                new IPerformanceTierApplier[]
                {
                    new TrackingTierApplier(5, "T5", calls),
                    new TrackingTierApplier(0, "T0", calls)
                });
            bool eventSawCommittedState = false;
            service.TierChanged += tier =>
            {
                eventSawCommittedState = tier == PerformanceTier.High
                    && service.CurrentProfile == high
                    && service.HasAppliedProfile;
            };

            try
            {
                service.ApplyTier(PerformanceTier.High);
                CollectionAssert.AreEqual(new[] { "R0", "R10", "T0", "T5" }, calls);
                Assert.That(eventSawCommittedState, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(high);
            }
        }

        [Test]
        public void SettingsCompatibilityPerformanceProperties_DoNotActuateRuntimeState()
        {
            var settings = new SettingsManager(new MemorySettingsStore());
            settings.Initialize();
            int fpsBefore = Application.targetFrameRate;
            int qualityBefore = QualitySettings.GetQualityLevel();

            settings.TargetFrameRate = fpsBefore == 30 ? 60 : 30;
            settings.QualityLevel = qualityBefore + 1;

            Assert.That(Application.targetFrameRate, Is.EqualTo(fpsBefore));
            Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(qualityBefore));
        }

        [Test]
        public void ManualPreferencePersists_AndAutoDoesNotEraseLastManualTier()
        {
            var store = new MemorySettingsStore();
            var settings = new SettingsManager(store);
            settings.Initialize();
            var performance = new FakePerformanceService();
            var classifier = new FakeClassifier(PerformanceTier.Low);
            var coordinator = new PerformancePreferenceCoordinator(settings, performance, classifier);

            coordinator.SelectTier(PerformanceTier.High);
            settings.Save();
            Assert.That(settings.UseAutomaticPerformanceTier, Is.False);
            Assert.That(settings.PreferredPerformanceTier, Is.EqualTo((int)PerformanceTier.High));
            Assert.That(performance.LastApplied, Is.EqualTo(PerformanceTier.High));

            var reloaded = new SettingsManager(store);
            reloaded.Initialize();
            var secondPerformance = new FakePerformanceService();
            var secondCoordinator = new PerformancePreferenceCoordinator(reloaded, secondPerformance, classifier);
            secondCoordinator.Initialize();
            Assert.That(secondPerformance.LastApplied, Is.EqualTo(PerformanceTier.High));

            secondCoordinator.UseAutomatic();
            Assert.That(secondPerformance.LastApplied, Is.EqualTo(PerformanceTier.Low));
            Assert.That(reloaded.UseAutomaticPerformanceTier, Is.True);
            Assert.That(reloaded.PreferredPerformanceTier, Is.EqualTo((int)PerformanceTier.High));
        }

        private static PerformanceProfileSO CreateProfile(PerformanceTier tier, int fps, int quality, float lodBias)
        {
            PerformanceProfileSO profile = ScriptableObject.CreateInstance<PerformanceProfileSO>();
            SetField(profile, "tier", tier);
            SetField(profile, "targetFrameRate", fps);
            SetField(profile, "unityQualityLevel", quality);
            SetField(profile, "lodBias", lodBias);
            return profile;
        }

        private static PerformanceCatalogSO CreateCatalog(PerformanceTier defaultTier, params PerformanceProfileSO[] profiles)
        {
            PerformanceCatalogSO catalog = ScriptableObject.CreateInstance<PerformanceCatalogSO>();
            SetField(catalog, "defaultTier", defaultTier);
            SetField(catalog, "profiles", profiles);
            return catalog;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private sealed class TrackingRendererApplier : IPerformanceRendererApplier
        {
            private readonly string name;
            private readonly List<string> calls;
            public TrackingRendererApplier(int order, string name, List<string> calls) { Order = order; this.name = name; this.calls = calls; }
            public int Order { get; }
            public void Apply(PerformanceProfileSO profile) => calls.Add(name);
        }

        private sealed class TrackingTierApplier : IPerformanceTierApplier
        {
            private readonly string name;
            private readonly List<string> calls;
            public TrackingTierApplier(int order, string name, List<string> calls = null) { Order = order; this.name = name; this.calls = calls; }
            public int Order { get; }
            public int Count { get; private set; }
            public void Apply(PerformanceProfileSO profile) { Count++; calls?.Add(name); }
        }

        private sealed class FakeClassifier : IDevicePerformanceClassifier
        {
            private readonly PerformanceTier tier;
            public FakeClassifier(PerformanceTier tier) => this.tier = tier;
            public PerformanceTier RecommendTier() => tier;
        }

        private sealed class FakePerformanceService : IPerformanceService
        {
            public PerformanceTier LastApplied { get; private set; } = PerformanceTier.Custom;
            public PerformanceTier CurrentTier => LastApplied;
            public PerformanceProfileSO CurrentProfile => null;
            public int TargetFrameRate => 0;
            public bool HasAppliedProfile => LastApplied != PerformanceTier.Custom;
            public event Action<PerformanceTier> TierChanged;
            public void ApplyTier(PerformanceTier tier) { LastApplied = tier; TierChanged?.Invoke(tier); }
            public void Reapply() { }
        }

        private sealed class MemorySettingsStore : ISettingsStore
        {
            private readonly Dictionary<string, object> values = new Dictionary<string, object>();
            public bool HasKey(string key, string section = null) => values.ContainsKey(Key(key, section));
            public bool GetBool(string key, bool defaultValue = false, string section = null) => Get(key, defaultValue, section);
            public int GetInt(string key, int defaultValue = 0, string section = null) => Get(key, defaultValue, section);
            public float GetFloat(string key, float defaultValue = 0f, string section = null) => Get(key, defaultValue, section);
            public string GetString(string key, string defaultValue = "", string section = null) => Get(key, defaultValue, section);
            public void SetBool(string key, bool value, string section = null) => Set(key, value, section);
            public void SetInt(string key, int value, string section = null) => Set(key, value, section);
            public void SetFloat(string key, float value, string section = null) => Set(key, value, section);
            public void SetString(string key, string value, string section = null) => Set(key, value, section);
            public bool Delete(string key, string section = null) => values.Remove(Key(key, section));
            public void Save() { }
            private T Get<T>(string key, T fallback, string section) => values.TryGetValue(Key(key, section), out object value) && value is T typed ? typed : fallback;
            private void Set<T>(string key, T value, string section) => values[Key(key, section)] = value;
            private static string Key(string key, string section) => string.IsNullOrEmpty(section) ? key : section + "/" + key;
        }
    }
}
