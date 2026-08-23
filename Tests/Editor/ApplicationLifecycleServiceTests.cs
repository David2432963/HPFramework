using System;
using System.Reflection;
using HP.Framework.Lifecycle;
using NUnit.Framework;
using UnityEngine;

namespace HP.Framework.Tests
{
    public sealed class ApplicationLifecycleServiceTests
    {
        [Test]
        public void LifecycleSignals_AreIdempotentForStateTransitions()
        {
            GameObject owner = new GameObject("LifecycleTests");
            ApplicationLifecycleService service = owner.AddComponent<ApplicationLifecycleService>();
            int paused = 0;
            int resumed = 0;
            int focus = 0;
            int lowMemory = 0;
            int quitting = 0;

            service.Paused += () => paused++;
            service.Resumed += () => resumed++;
            service.FocusChanged += _ => focus++;
            service.LowMemory += () => lowMemory++;
            service.Quitting += () => quitting++;

            try
            {
                Invoke(service, "OnApplicationPause", true);
                Invoke(service, "OnApplicationPause", true);
                Invoke(service, "OnApplicationPause", false);
                Invoke(service, "OnApplicationPause", false);
                Invoke(service, "OnApplicationFocus", false);
                Invoke(service, "OnApplicationFocus", false);
                Invoke(service, "OnApplicationFocus", true);
                service.SignalLowMemory();
                service.SignalLowMemory();
                Invoke(service, "OnApplicationQuit");
                Invoke(service, "OnApplicationQuit");

                Assert.That(paused, Is.EqualTo(1));
                Assert.That(resumed, Is.EqualTo(1));
                Assert.That(focus, Is.EqualTo(2));
                Assert.That(lowMemory, Is.EqualTo(2));
                Assert.That(quitting, Is.EqualTo(1));
                Assert.That(service.IsPaused, Is.False);
                Assert.That(service.HasFocus, Is.True);
            }
            finally
            {
                service.Dispose();
                service.Dispose();
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static void Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, args);
        }
    }
}
