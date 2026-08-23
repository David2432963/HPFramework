using System.Collections;
using System.Reflection;
using HP.Framework.Assets;
using HP.Framework.Bootstrap;
using HP.Framework.Lifecycle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace HP.Framework.Tests
{
    public sealed class ApplicationLifecycleAssetMemoryPlayModeTests
    {
        private sealed class FakeAssetMemoryService : IAssetMemoryService
        {
            public int LoadedAssetCount => 0;
            public int UnusedAssetCount => 0;
            public int TrimCount { get; private set; }
            public void TrimUnused() => TrimCount++;
        }

        private sealed class TestRootLifetimeScope : RootLifetimeScope
        {
            private readonly FakeAssetMemoryService memory = new FakeAssetMemoryService();

            public int TrimCount => memory.TrimCount;

            protected override void RegisterAssetProvider(IContainerBuilder builder)
            {
                builder.RegisterInstance(memory).As<IAssetMemoryService>();
                RegisterAssetMemoryLifecycleIntegration(builder);
            }
        }

        [UnityTest]
        public IEnumerator LowMemorySignal_TrimsThroughCompositionAdapter()
        {
            GameObject root = new GameObject("AssetMemoryLifecycleRoot");
            root.SetActive(false);
            TestRootLifetimeScope scope = root.AddComponent<TestRootLifetimeScope>();
            scope.autoRun = false;
            GameObject lifecycleOwner = new GameObject("Lifecycle");
            lifecycleOwner.transform.SetParent(root.transform, false);
            ApplicationLifecycleService lifecycle = lifecycleOwner.AddComponent<ApplicationLifecycleService>();
            SetField(scope, "applicationLifecycle", lifecycle);
            root.SetActive(true);

            scope.Build();
            Invoke(lifecycle, "SignalLowMemory");
            Assert.That(scope.TrimCount, Is.EqualTo(1));

            scope.DisposeCore();
            Invoke(lifecycle, "SignalLowMemory");
            Assert.That(scope.TrimCount, Is.EqualTo(1), "Disposed adapter must unsubscribe.");

            Object.Destroy(root);
            yield return null;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = typeof(RootLifetimeScope).GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
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
