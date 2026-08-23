using System.Collections;
using System.Reflection;
using HP.Framework.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HP.Framework.Tests
{
    public sealed class MobileDiagnosticsHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator DisableAndReenable_ReleasesAndRecreatesSampler()
        {
            GameObject owner = new GameObject("MobileDiagnosticsHud_Test");
            FpsDisplay display = owner.AddComponent<FpsDisplay>();
            yield return null;

            Assert.That(GetSampler(display), Is.Not.Null);
            owner.SetActive(false);
            Assert.That(GetSampler(display), Is.Null);

            owner.SetActive(true);
            Assert.That(GetSampler(display), Is.Not.Null);

            Object.Destroy(owner);
            yield return null;
        }

        private static MobileDiagnosticsSampler GetSampler(FpsDisplay display)
        {
            return typeof(FpsDisplay)
                .GetField("sampler", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(display) as MobileDiagnosticsSampler;
        }
    }
}
