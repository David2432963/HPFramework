using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using HP.Framework.Bootstrap;
using HP.Framework.Lifecycle;
using HP.Framework.Pooling;
using NUnit.Framework;
using UnityEngine;

namespace HP.Framework.Tests.Editor
{
    public sealed class PoolServiceTests
    {
        private sealed class FakeLifecycle : IApplicationLifecycle
        {
            public bool IsPaused => false;
            public bool HasFocus => true;

            public event Action Paused;
            public event Action Resumed;
            public event Action<bool> FocusChanged;
            public event Action LowMemory;
            public event Action Quitting;

            public void RaiseLowMemory()
            {
                LowMemory?.Invoke();
            }
        }

        private GameObject managerObject;
        private GameObject prefab;
        private PoolManager manager;

        [SetUp]
        public void SetUp()
        {
            managerObject = new GameObject("PoolManager_Test");
            manager = managerObject.AddComponent<PoolManager>();
            manager.Initialize();
            prefab = new GameObject("PoolPrefab_Test");
        }

        [TearDown]
        public void TearDown()
        {
            manager?.Dispose();
            if (managerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }

            if (prefab != null)
            {
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public async Task PrewarmAsync_RespectsBatchSize_AndCompletes()
        {
            UniTask prewarm = manager.PrewarmAsync(
                prefab,
                5,
                new PoolPrewarmOptions(maxInstancesPerFrame: 2));

            Assert.That(manager.Stats.InactiveInstanceCount, Is.EqualTo(2));
            await prewarm;

            Assert.That(manager.Stats.InactiveInstanceCount, Is.EqualTo(5));
            Assert.That(manager.Stats.CreatedTotal, Is.EqualTo(5));
        }

        [Test]
        public async Task PrewarmAsync_CancellationLeavesPoolUsable()
        {
            using var cancellation = new CancellationTokenSource();
            UniTask prewarm = manager.PrewarmAsync(
                prefab,
                10,
                new PoolPrewarmOptions(maxInstancesPerFrame: 1),
                cancellation.Token);

            Assert.That(manager.Stats.InactiveInstanceCount, Is.EqualTo(1));
            cancellation.Cancel();

            OperationCanceledException exception = null;
            try
            {
                await prewarm;
            }
            catch (OperationCanceledException caught)
            {
                exception = caught;
            }

            Assert.That(exception, Is.Not.Null);
            GameObject instance = manager.Get(prefab);
            manager.Release(instance);
            Assert.That(manager.Stats.InactiveInstanceCount, Is.EqualTo(1));
        }

        [Test]
        public void PerPoolCapacity_DestroysOverflowOnRelease()
        {
            manager.SetMaxInactive(prefab, 2);
            GameObject first = manager.Get(prefab);
            GameObject second = manager.Get(prefab);
            GameObject third = manager.Get(prefab);

            manager.Release(first);
            manager.Release(second);
            manager.Release(third);

            Assert.That(manager.TryGetStats(prefab, out PoolStats stats), Is.True);
            Assert.That(stats.MaxInactiveCount, Is.EqualTo(2));
            Assert.That(stats.InactiveInstanceCount, Is.EqualTo(2));
            Assert.That(stats.TrimmedTotal, Is.EqualTo(1));
        }

        [Test]
        public void Trim_NeverDestroysActiveInstances()
        {
            manager.Prewarm(prefab, 5);
            GameObject first = manager.Get(prefab);
            GameObject second = manager.Get(prefab);

            manager.Trim(prefab, 1);

            Assert.That(manager.TryGetStats(prefab, out PoolStats stats), Is.True);
            Assert.That(stats.ActiveInstanceCount, Is.EqualTo(2));
            Assert.That(stats.InactiveInstanceCount, Is.EqualTo(1));
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
        }

        [Test]
        public void DuplicateRelease_DoesNotDuplicateInactiveEntry()
        {
            GameObject instance = manager.Get(prefab);
            manager.Release(instance);
            manager.Release(instance);

            Assert.That(manager.Stats.InactiveInstanceCount, Is.EqualTo(1));
        }

        [Test]
        public void LowMemory_TrimsInactiveOnly()
        {
            manager.Prewarm(prefab, 4);
            GameObject active = manager.Get(prefab);
            var lifecycle = new FakeLifecycle();
            var trimmer = new ApplicationLifecyclePoolTrimmer(lifecycle, manager);
            trimmer.Initialize();

            lifecycle.RaiseLowMemory();

            Assert.That(manager.Stats.ActiveInstanceCount, Is.EqualTo(1));
            Assert.That(manager.Stats.InactiveInstanceCount, Is.Zero);
            Assert.That(active, Is.Not.Null);
            trimmer.Dispose();
        }

        [Test]
        public void SpawnReleaseStress_ReusesInstanceAndStableMetadata()
        {
            for (int i = 0; i < 1000; i++)
            {
                GameObject instance = manager.Get(prefab);
                manager.Release(instance);
            }

            Assert.That(manager.Stats.ActiveInstanceCount, Is.Zero);
            Assert.That(manager.Stats.InactiveInstanceCount, Is.EqualTo(1));
            Assert.That(manager.Stats.CreatedTotal, Is.EqualTo(1));
        }
    }
}
