using System.Collections;
using System.Reflection;
using HP.Framework.Bootstrap;
using HP.Framework.Lifecycle;
using HP.Framework.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace HP.Framework.Tests
{
    public sealed class ApplicationLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator RootLifecycle_PauseFlushesDirtySettings_DisposeUnsubscribes_AndRootCanRebuild()
        {
            CreateRoot(out GameObject firstRoot, out RootLifetimeScope firstScope, out ApplicationLifecycleService firstLifecycle);
            firstScope.Build();

            IApplicationLifecycle resolvedLifecycle = firstScope.Container.Resolve<IApplicationLifecycle>();
            ISettingsService settings = firstScope.Container.Resolve<ISettingsService>();
            ISettingsFlushService flush = firstScope.Container.Resolve<ISettingsFlushService>();

            Assert.That(resolvedLifecycle, Is.SameAs(firstLifecycle));
            settings.SoundEnabled = !settings.SoundEnabled;
            Assert.That(flush.IsDirty, Is.True);

            Invoke(firstLifecycle, "OnApplicationPause", true);
            Assert.That(flush.IsDirty, Is.False);

            firstScope.DisposeCore();
            Assert.That(firstLifecycle.IsLowMemorySubscribed, Is.False);
            UnityEngine.Object.Destroy(firstRoot);
            yield return null;

            CreateRoot(out GameObject secondRoot, out RootLifetimeScope secondScope, out ApplicationLifecycleService secondLifecycle);
            secondScope.Build();
            Assert.That(secondScope.Container.Resolve<IApplicationLifecycle>(), Is.SameAs(secondLifecycle));

            secondScope.DisposeCore();
            Assert.That(secondLifecycle.IsLowMemorySubscribed, Is.False);
            UnityEngine.Object.Destroy(secondRoot);
            yield return null;
        }

        private static void CreateRoot(
            out GameObject root,
            out RootLifetimeScope scope,
            out ApplicationLifecycleService lifecycle)
        {
            root = new GameObject("LifecycleRootTest");
            root.SetActive(false);
            scope = root.AddComponent<RootLifetimeScope>();
            scope.autoRun = false;
            GameObject lifecycleOwner = new GameObject("Lifecycle");
            lifecycleOwner.transform.SetParent(root.transform, false);
            lifecycle = lifecycleOwner.AddComponent<ApplicationLifecycleService>();
            SetField(scope, "applicationLifecycle", lifecycle);
            root.SetActive(true);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static void Invoke(object target, string name, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(target, args);
        }

    }
}
