using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using HP.Framework.Assets;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HP.Framework.Tests
{
    public sealed class ResourcesAssetProviderLeaseTests
    {
        private sealed class TestAsset : ScriptableObject { }
        private sealed class OtherAsset : ScriptableObject { }

        private sealed class FakeLoader : IResourcesAssetLoader
        {
            private readonly Dictionary<string, Object> assets =
                new Dictionary<string, Object>(StringComparer.Ordinal);
            private UniTaskCompletionSource<Object> deferredCompletion;

            public int LoadCount { get; private set; }
            public int UnloadCount { get; private set; }
            public int FailuresRemaining { get; set; }
            public bool DeferLoads { get; set; }
            public List<Object> UnloadedAssets { get; } = new List<Object>();

            public void SetAsset(string key, Object asset)
            {
                assets[key] = asset;
            }

            public async UniTask<Object> LoadAsync(string key, Type requestedType)
            {
                LoadCount++;
                if (FailuresRemaining > 0)
                {
                    FailuresRemaining--;
                    throw new InvalidOperationException("simulated load failure");
                }

                if (DeferLoads)
                {
                    deferredCompletion ??= new UniTaskCompletionSource<Object>();
                    return await deferredCompletion.Task;
                }

                assets.TryGetValue(key, out Object asset);
                return asset;
            }

            public void CompleteDeferred(Object asset)
            {
                Assert.That(deferredCompletion, Is.Not.Null, "No deferred load is pending.");
                deferredCompletion.TrySetResult(asset);
            }

            public void Unload(Object asset)
            {
                UnloadCount++;
                UnloadedAssets.Add(asset);
            }
        }

        [Test]
        public async Task ConcurrentSameKeyAcquire_SharesOneLoad_AndRefCounts()
        {
            TestAsset asset = ScriptableObject.CreateInstance<TestAsset>();
            var loader = new FakeLoader { DeferLoads = true };
            var provider = new ResourcesAssetProvider(loader, null);
            try
            {
                UniTask<IAssetLease<TestAsset>> firstTask = provider.AcquireAsync<TestAsset>("asset");
                UniTask<IAssetLease<TestAsset>> secondTask = provider.AcquireAsync<TestAsset>("asset");
                Assert.That(loader.LoadCount, Is.EqualTo(1));

                loader.CompleteDeferred(asset);
                IAssetLease<TestAsset> first = await firstTask;
                IAssetLease<TestAsset> second = await secondTask;
                Assert.That(provider.GetStats().ActiveReferenceCount, Is.EqualTo(2));

                first.Dispose();
                Assert.That(provider.GetStats().ActiveReferenceCount, Is.EqualTo(1));
                Assert.That(provider.UnusedAssetCount, Is.EqualTo(0));

                second.Dispose();
                Assert.That(provider.GetStats().ActiveReferenceCount, Is.EqualTo(0));
                Assert.That(provider.UnusedAssetCount, Is.EqualTo(1));

                provider.TrimUnused();
                Assert.That(loader.UnloadCount, Is.EqualTo(1));
                Assert.That(provider.LoadedAssetCount, Is.EqualTo(0));
            }
            finally
            {
                provider.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public async Task SameKeyIncompatibleType_FailsWithoutDuplicateLoad()
        {
            TestAsset asset = ScriptableObject.CreateInstance<TestAsset>();
            var loader = new FakeLoader();
            loader.SetAsset("asset", asset);
            var provider = new ResourcesAssetProvider(loader, null);
            try
            {
                IAssetLease<TestAsset> lease = await provider.AcquireAsync<TestAsset>("asset");
                InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await provider.AcquireAsync<OtherAsset>("asset"));

                Assert.That(exception.Message, Does.Contain("canonical type"));
                Assert.That(loader.LoadCount, Is.EqualTo(1));
                lease.Dispose();
            }
            finally
            {
                provider.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public async Task RawAndLeaseOwnership_DoNotStealEachOthersReferences()
        {
            TestAsset asset = ScriptableObject.CreateInstance<TestAsset>();
            var loader = new FakeLoader();
            loader.SetAsset("asset", asset);
            var provider = new ResourcesAssetProvider(loader, null);
            try
            {
                TestAsset raw = await provider.LoadAssetAsync<TestAsset>("asset");
                IAssetLease<TestAsset> lease = await provider.AcquireAsync<TestAsset>("asset");
                Assert.That(loader.LoadCount, Is.EqualTo(1));
                Assert.That(provider.GetStats().ActiveReferenceCount, Is.EqualTo(2));

                provider.ReleaseAsset(raw);
                provider.TrimUnused();
                Assert.That(loader.UnloadCount, Is.EqualTo(0));
                Assert.That(lease.IsValid, Is.True);

                lease.Dispose();
                provider.TrimUnused();
                Assert.That(loader.UnloadCount, Is.EqualTo(1));
            }
            finally
            {
                provider.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public async Task CancelledWaiter_DoesNotCancelSharedUnderlyingLoad()
        {
            TestAsset asset = ScriptableObject.CreateInstance<TestAsset>();
            var loader = new FakeLoader { DeferLoads = true };
            var provider = new ResourcesAssetProvider(loader, null);
            using var cancellation = new CancellationTokenSource();
            try
            {
                UniTask<IAssetLease<TestAsset>> cancelledTask =
                    provider.AcquireAsync<TestAsset>("asset", cancellation.Token);
                UniTask<IAssetLease<TestAsset>> survivorTask = provider.AcquireAsync<TestAsset>("asset");
                cancellation.Cancel();

                Assert.CatchAsync<OperationCanceledException>(async () => await cancelledTask);
                Assert.That(loader.LoadCount, Is.EqualTo(1));

                loader.CompleteDeferred(asset);
                IAssetLease<TestAsset> survivor = await survivorTask;
                Assert.That(survivor.Asset, Is.SameAs(asset));
                Assert.That(provider.GetStats().ActiveReferenceCount, Is.EqualTo(1));
                survivor.Dispose();
            }
            finally
            {
                provider.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public async Task AllWaitersCancelled_LoadFinishesAsUnusedNotPending()
        {
            TestAsset asset = ScriptableObject.CreateInstance<TestAsset>();
            var loader = new FakeLoader { DeferLoads = true };
            var provider = new ResourcesAssetProvider(loader, null);
            using var cancellation = new CancellationTokenSource();
            try
            {
                UniTask<IAssetLease<TestAsset>> task =
                    provider.AcquireAsync<TestAsset>("asset", cancellation.Token);
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>(async () => await task);

                loader.CompleteDeferred(asset);
                await UniTask.Yield();

                AssetProviderStats stats = provider.GetStats();
                Assert.That(stats.PendingLoadCount, Is.EqualTo(0));
                Assert.That(stats.ActiveReferenceCount, Is.EqualTo(0));
                Assert.That(stats.UnusedAssetCount, Is.EqualTo(1));
                provider.TrimUnused();
                Assert.That(loader.UnloadCount, Is.EqualTo(1));
            }
            finally
            {
                provider.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public async Task FailedLoad_IsRemovedAndCanRetry()
        {
            TestAsset asset = ScriptableObject.CreateInstance<TestAsset>();
            var loader = new FakeLoader { FailuresRemaining = 1 };
            loader.SetAsset("asset", asset);
            var provider = new ResourcesAssetProvider(loader, null);
            try
            {
                Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await provider.AcquireAsync<TestAsset>("asset"));

                IAssetLease<TestAsset> lease = await provider.AcquireAsync<TestAsset>("asset");
                Assert.That(loader.LoadCount, Is.EqualTo(2));
                Assert.That(lease.Asset, Is.SameAs(asset));
                lease.Dispose();
            }
            finally
            {
                provider.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public async Task TrimUnused_OnlyReleasesZeroReferenceRecords()
        {
            TestAsset activeAsset = ScriptableObject.CreateInstance<TestAsset>();
            TestAsset idleAsset = ScriptableObject.CreateInstance<TestAsset>();
            var loader = new FakeLoader();
            loader.SetAsset("active", activeAsset);
            loader.SetAsset("idle", idleAsset);
            var provider = new ResourcesAssetProvider(loader, null);
            try
            {
                IAssetLease<TestAsset> active = await provider.AcquireAsync<TestAsset>("active");
                IAssetLease<TestAsset> idle = await provider.AcquireAsync<TestAsset>("idle");
                idle.Dispose();

                provider.TrimUnused();
                CollectionAssert.Contains(loader.UnloadedAssets, idleAsset);
                CollectionAssert.DoesNotContain(loader.UnloadedAssets, activeAsset);
                Assert.That(active.IsValid, Is.True);

                active.Dispose();
                provider.TrimUnused();
                CollectionAssert.Contains(loader.UnloadedAssets, activeAsset);
            }
            finally
            {
                provider.Dispose();
                Object.DestroyImmediate(activeAsset);
                Object.DestroyImmediate(idleAsset);
            }
        }

        [Test]
        public async Task ProviderDispose_InvalidatesActiveLease_AndForceReleasesAsset()
        {
            TestAsset asset = ScriptableObject.CreateInstance<TestAsset>();
            var loader = new FakeLoader();
            loader.SetAsset("asset", asset);
            var provider = new ResourcesAssetProvider(loader, null);
            try
            {
                IAssetLease<TestAsset> lease = await provider.AcquireAsync<TestAsset>("asset");
                Assert.That(lease.IsValid, Is.True);

                provider.Dispose();
                Assert.That(lease.IsValid, Is.False);
                Assert.That(loader.UnloadCount, Is.EqualTo(1));
                lease.Dispose();
            }
            finally
            {
                provider.Dispose();
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public async Task ThousandLeases_ShareOneLoad_AndReturnToZeroReferences()
        {
            TestAsset asset = ScriptableObject.CreateInstance<TestAsset>();
            var loader = new FakeLoader();
            loader.SetAsset("asset", asset);
            var provider = new ResourcesAssetProvider(loader, null);
            var leases = new List<IAssetLease<TestAsset>>(1000);
            try
            {
                for (int i = 0; i < 1000; i++)
                {
                    leases.Add(await provider.AcquireAsync<TestAsset>("asset"));
                }

                Assert.That(loader.LoadCount, Is.EqualTo(1));
                Assert.That(provider.GetStats().ActiveReferenceCount, Is.EqualTo(1000));

                for (int i = 0; i < leases.Count; i++)
                {
                    leases[i].Dispose();
                }

                Assert.That(provider.GetStats().ActiveReferenceCount, Is.EqualTo(0));
                Assert.That(provider.UnusedAssetCount, Is.EqualTo(1));
            }
            finally
            {
                provider.Dispose();
                Object.DestroyImmediate(asset);
            }
        }
    }
}
