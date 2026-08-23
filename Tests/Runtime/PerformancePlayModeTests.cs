using System.Collections;
using System.Reflection;
using HP.Framework.Bootstrap;
using HP.Framework.Performance;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace HP.Framework.Tests
{
    public sealed class PerformancePlayModeTests
    {
        [UnityTest]
        public IEnumerator RootScope_ResolvesAndAppliesPerformancePolicy()
        {
            PerformanceProfileSO low = CreateProfile(PerformanceTier.Low, 30, 0, 0.7f);
            PerformanceProfileSO medium = CreateProfile(PerformanceTier.Medium, 60, 0, 1f);
            PerformanceProfileSO high = CreateProfile(PerformanceTier.High, 60, 0, 1.25f);
            PerformanceCatalogSO catalog = ScriptableObject.CreateInstance<PerformanceCatalogSO>();
            SetField(catalog, "defaultTier", PerformanceTier.Medium);
            SetField(catalog, "profiles", new[] { low, medium, high });
            DevicePerformancePolicySO policy = ScriptableObject.CreateInstance<DevicePerformancePolicySO>();

            GameObject root = new GameObject("PerformanceRootScopeTest");
            root.SetActive(false);
            RootLifetimeScope scope = root.AddComponent<RootLifetimeScope>();
            scope.autoRun = false;
            SetField(scope, "performanceCatalog", catalog);
            SetField(scope, "devicePerformancePolicy", policy);
            root.SetActive(true);

            try
            {
                scope.Build();
                IPerformanceService performance = scope.Container.Resolve<IPerformanceService>();
                IPerformancePreferenceService preferences = scope.Container.Resolve<IPerformancePreferenceService>();

                Assert.That(performance, Is.Not.Null);
                Assert.That(preferences, Is.Not.Null);
                Assert.That(performance.HasAppliedProfile, Is.True);
                Assert.That(performance.CurrentProfile, Is.Not.Null);
            }
            finally
            {
                scope.DisposeCore();
                Object.Destroy(root);
                Object.Destroy(low);
                Object.Destroy(medium);
                Object.Destroy(high);
                Object.Destroy(catalog);
                Object.Destroy(policy);
            }

            yield return null;
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

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
    }
}
