using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using HP.Framework.Assets;
using NUnit.Framework;
using UnityEngine;

namespace HP.Framework.Tests
{
    public sealed class AssetOwnershipTests
    {
        private sealed class FakeResourcesAssetLoader : IResourcesAssetLoader
        {
            private readonly Dictionary<string, UnityEngine.Object> assets =
                new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
            private readonly Dictionary<string, UniTaskCompletionSource<UnityEngine.Object>> pending =
                new Dictionary<string, UniTaskCompletionSource<UnityEngine.Object>>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> failuresRemaining =
                new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> loadCounts =
                new Dictionary<string, int>(StringComparer.Ordinal);

            public int UnloadCount { get; private set; }

            public void SetAsset(string key, UnityEngine.Object asset)
            {
                assets[key] = asset;
            }

            public void Hold(string key, UnityEngine.Object asset)
            {
                assets[key] = asset;
                pending[key] = new UniTaskCompletionSource<UnityEngine.Object>();
            }

            public void Complete(string key)
            {
                UniTaskCompletionSource<UnityEngine.Object> source = pending[key];
                pending.Remove(key);
                source.TrySetResult(assets[key]);
            }

            public void FailNext(string key, int count = 1)
            {
                failuresRemaining[key] = count;
            }

            public int GetLoadCount(string key)
            {
                return loadCounts.TryGetValue(key, out int count) ? count : 0;
            }

            public UniTask<UnityEngine.Object> LoadAsync(string key, Type requestedType)
            {
                loadCounts[key] = GetLoadCount(key) + 1;
                if (failuresRemaining.TryGetValue(key, out int remaining) && remaining > 0)
                {
                    failuresRemaining[key] = remaining - 1;
                    return UniTask.FromException<UnityEngine.Object>(
                        new InvalidOperationException("Expected fake load failure."));
                }

                if (pending.TryGetValue(key, out UniTaskCompletionSource<UnityEngine.Object> source))
                {
                    return source.Task;
                }

                assets.TryGetValue(key, out UnityEngine.Object asset);
                return UniTask.FromResult(asset);
            }

            public void Unload(UnityEngine.Object asset)
            {
                UnloadCount++;
            }
        }

        [Test]
        public async Task ConcurrentAcquire_SameKey_LoadsOnce_AndReferenceCounts()
        {
            TextAsset asset = new TextAsset("shared");
            var loader = new FakeResourcesAssetLoader();
            loader.Hold("shared", asset);
            using var provider = new ResourcesAssetProvider(loader);

            Task<IAssetLease<TextAsset>> firstTask = provider.AcquireAsync<TextAsset>("shared").AsTask();
            Task<IAssetLease<TextAsset>> secondTask = provider.AcquireAsync<TextAsset>("shared").AsTask();

            Assert.That(loader.GetLoadCount("shared"), Is.EqualTo(1));
            Assert.That(provider.GetStats().PendingLoadCount, Is.EqualTo(1));
            loader.Complete("shared");

            IAssetLease<TextAsset> first = await firstTask;
            IAssetLease<TextAsset> second = await secondTask;
            Assert.That(first.Asset, Is.SameAs(asset));
            Assert.That(second.Asset, Is.SameAs(asset));
            Assert.That(provider.GetStats().ActiveReferenceCount, Is.EqualTo(2));

            first.Dispose();
            Assert.That(provider.GetStats().ActiveReferenceCount, Is.EqualTo(1));
            provider.TrimUnused();
            Assert.That(loader.UnloadCount, Is.Zero);

            second.Dispose();
            Assert.That(provider.UnusedAssetCount, Is.EqualTo(1));
            provider.TrimUnused();
            Assert.That(loader.UnloadCount, Is.EqualTo(1));
            Assert.That(provider.LoadedAssetCount, Is.Zero);

            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public async Task CanonicalType_CompatibleBaseType_ReusesSameRecord()
        {
            TextAsset asset = new TextAsset("typed");
            var loader = new FakeResourcesAssetLoader();
            loader.SetAsset("typed", asset);
            using var provider = new ResourcesAssetProvider(loader);

            IAssetLease<TextAsset> typed = await provider.AcquireAsync<TextAsset>("typed").AsTask();
            IAssetLease<UnityEngine.Object> asObject =
                await provider.AcquireAsync<UnityEngine.Object>("typed").AsTask();

            Assert.That(loader.GetLoadCount("typed"), Is.EqualTo(1));
            Assert.That(asObject.Asset, Is.SameAs(asset));
            Assert.That(provider.GetStats().ActiveReferenceCount, Is.EqualTo(2));

            typed.Dispose();
            asObject.Dispose();
            provider.TrimUnused();
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public async Task CanonicalType_IncompatibleRequest_FailsWithoutDuplicateLoad()
        {
            TextAsset asset = new TextAsset("typed");
            var loader = new FakeResourcesAssetLoader();
            loader.SetAsset("typed", asset);
            using var provider = new ResourcesAssetProvider(loader);

            IAssetLease<TextAsset> lease = await provider.AcquireAsync<TextAsset>("typed").AsTask();
            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await provider.AcquireAsync<GameObject>("typed").AsTask());

            StringAssert.Contains("canonical type", exception.Message);
            StringAssert.Contains("GameObject", exception.Message);
            Assert.That(loader.GetLoadCount("typed"), Is.EqualTo(1));
            lease.Dispose();
            provider.TrimUnused();
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public async Task DoubleDispose_IsSafe_AndTrimUnloadsOnce()
        {
            TextAsset asset = new TextAsset("double");
            var loader = new FakeResourcesAssetLoader();
            loader.SetAsset("double", asset);
            using var provider = new ResourcesAssetProvider(loader);

            IAssetLease<TextAsset> lease = await provider.AcquireAsync<TextAsset>("double").AsTask();
            lease.Dispose();
            lease.Dispose();

            Assert.That(provider.GetStats().ActiveReferenceCount, Is.Zero);
            Assert.That(provider.UnusedAssetCount, Is.EqualTo(1));
            provider.TrimUnused();
            provider.TrimUnused();
            Assert.That(loader.UnloadCount, Is.EqualTo(1));

            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public async Task FailedLoad_IsRemovedAndCanRetry()
        {
            TextAsset asset = new TextAsset("retry");
            var loader = new FakeResourcesAssetLoader();
            loader.SetAsset("retry", asset);
            loader.FailNext("retry");
            using var provider = new ResourcesAssetProvider(loader);

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await provider.AcquireAsync<TextAsset>("retry").AsTask());
            Assert.That(provider.GetStats().LoadedAssetCount, Is.Zero);
            Assert.That(provider.GetStats().PendingLoadCount, Is.Zero);

            IAssetLease<TextAsset> lease = await provider.AcquireAsync<TextAsset>("retry").AsTask();
            Assert.That(loader.GetLoadCount("retry"), Is.EqualTo(2));
            Assert.That(lease.Asset, Is.SameAs(asset));

            lease.Dispose();
            provider.TrimUnused();
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public async Task CancelledWaiter_DoesNotCancelSharedLoadOrAddReference()
        {
            TextAsset asset = new TextAsset("cancel-one");
            var loader = new FakeResourcesAssetLoader();
            loader.Hold("cancel-one", asset);
            using var provider = new ResourcesAssetProvider(loader);
            using var cancellation = new CancellationTokenSource();

            Task<IAssetLease<TextAsset>> ownerTask =
                provider.AcquireAsync<TextAsset>("cancel-one").AsTask();
            Task<IAssetLease<TextAsset>> cancelledTask =
                provider.AcquireAsync<TextAsset>("cancel-one", cancellation.Token).AsTask();
            cancellation.Cancel();

            try
            {
                await cancelledTask;
                Assert.Fail("Cancelled waiter unexpectedly acquired a lease.");
            }
            catch (OperationCanceledException)
            {
            }

            loader.Complete("cancel-one");
            IAssetLease<TextAsset> owner = await ownerTask;
            Assert.That(loader.GetLoadCount("cancel-one"), Is.EqualTo(1));
            Assert.That(provider.GetStats().ActiveReferenceCount, Is.EqualTo(1));

            owner.Dispose();
            provider.TrimUnused();
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public async Task AllWaitersCancelled_LoadFinishesAsTrimEligibleWithoutLiveLease()
        {
            TextAsset asset = new TextAsset("cancel-all");
            var loader = new FakeResourcesAssetLoader();
            loader.Hold("cancel-all", asset);
            using var provider = new ResourcesAssetProvider(loader);
            using var firstCancellation = new CancellationTokenSource();
            using var secondCancellation = new CancellationTokenSource();

            Task<IAssetLease<TextAsset>> first =
                provider.AcquireAsync<TextAsset>("cancel-all", firstCancellation.Token).AsTask();
            Task<IAssetLease<TextAsset>> second =
                provider.AcquireAsync<TextAsset>("cancel-all", secondCancellation.Token).AsTask();
            firstCancellation.Cancel();
            secondCancellation.Cancel();

            await ExpectCancellation(first);
            await ExpectCancellation(second);
            Assert.That(provider.GetStats().ActiveReferenceCount, Is.Zero);
            Assert.That(provider.GetStats().PendingLoadCount, Is.EqualTo(1));

            loader.Complete("cancel-all");
            Assert.That(provider.GetStats().PendingLoadCount, Is.Zero);
            Assert.That(provider.GetStats().ActiveReferenceCount, Is.Zero);
            Assert.That(provider.UnusedAssetCount, Is.EqualTo(1));

            provider.TrimUnused();
            Assert.That(provider.LoadedAssetCount, Is.Zero);
            Assert.That(loader.UnloadCount, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public async Task CompatibilityAndLeaseOwnership_DoNotStealEachOthersReferences()
        {
            TextAsset asset = new TextAsset("mixed");
            var loader = new FakeResourcesAssetLoader();
            loader.SetAsset("mixed", asset);
            using var provider = new ResourcesAssetProvider(loader);

            IAssetLease<TextAsset> lease = await provider.AcquireAsync<TextAsset>("mixed").AsTask();
            TextAsset compatibility = await provider.LoadAssetAsync<TextAsset>("mixed").AsTask();
            Assert.That(loader.GetLoadCount("mixed"), Is.EqualTo(1));
            Assert.That(provider.GetStats().ActiveReferenceCount, Is.EqualTo(2));

            provider.ReleaseAsset(compatibility);
            Assert.That(provider.GetStats().ActiveReferenceCount, Is.EqualTo(1));
            Assert.That(loader.UnloadCount, Is.Zero);

            lease.Dispose();
            Assert.That(provider.UnusedAssetCount, Is.EqualTo(1));
            provider.TrimUnused();
            Assert.That(loader.UnloadCount, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public async Task ProviderDispose_InvalidatesActiveLeaseAndForceReleasesOwnedAsset()
        {
            TextAsset asset = new TextAsset("dispose");
            var loader = new FakeResourcesAssetLoader();
            loader.SetAsset("dispose", asset);
            var provider = new ResourcesAssetProvider(loader);

            IAssetLease<TextAsset> lease = await provider.AcquireAsync<TextAsset>("dispose").AsTask();
            Assert.That(lease.IsValid, Is.True);

            provider.Dispose();
            Assert.That(lease.IsValid, Is.False);
            Assert.That(lease.Asset, Is.Null);
            Assert.That(loader.UnloadCount, Is.EqualTo(1));
            lease.Dispose();
            Assert.That(loader.UnloadCount, Is.EqualTo(1));

            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public async Task ThousandLeaseStress_UsesOneLoad_AndReleasesAllReferences()
        {
            TextAsset asset = new TextAsset("stress");
            var loader = new FakeResourcesAssetLoader();
            loader.SetAsset("stress", asset);
            using var provider = new ResourcesAssetProvider(loader);
            var leases = new IAssetLease<TextAsset>[1000];

            for (int i = 0; i < leases.Length; i++)
            {
                leases[i] = await provider.AcquireAsync<TextAsset>("stress").AsTask();
            }

            AssetProviderStats loadedStats = provider.GetStats();
            Assert.That(loader.GetLoadCount("stress"), Is.EqualTo(1));
            Assert.That(loadedStats.LoadedAssetCount, Is.EqualTo(1));
            Assert.That(loadedStats.ActiveReferenceCount, Is.EqualTo(1000));

            for (int i = 0; i < leases.Length; i++)
            {
                leases[i].Dispose();
            }

            Assert.That(provider.GetStats().ActiveReferenceCount, Is.Zero);
            Assert.That(provider.UnusedAssetCount, Is.EqualTo(1));
            provider.TrimUnused();
            Assert.That(loader.UnloadCount, Is.EqualTo(1));
            Assert.That(provider.LoadedAssetCount, Is.Zero);

            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public async Task AssetLeaseScope_DisposesTrackedLeasesInOneOwnershipBoundary()
        {
            TextAsset firstAsset = new TextAsset("scope-a");
            TextAsset secondAsset = new TextAsset("scope-b");
            var loader = new FakeResourcesAssetLoader();
            loader.SetAsset("scope-a", firstAsset);
            loader.SetAsset("scope-b", secondAsset);
            using var provider = new ResourcesAssetProvider(loader);
            var scope = new AssetLeaseScope();

            scope.Track(await provider.AcquireAsync<TextAsset>("scope-a").AsTask());
            scope.Track(await provider.AcquireAsync<TextAsset>("scope-b").AsTask());
            Assert.That(provider.GetStats().ActiveReferenceCount, Is.EqualTo(2));

            scope.Dispose();
            scope.Dispose();
            Assert.That(provider.GetStats().ActiveReferenceCount, Is.Zero);
            Assert.That(provider.UnusedAssetCount, Is.EqualTo(2));
            provider.TrimUnused();
            Assert.That(loader.UnloadCount, Is.EqualTo(2));

            UnityEngine.Object.DestroyImmediate(firstAsset);
            UnityEngine.Object.DestroyImmediate(secondAsset);
        }

        private static async Task ExpectCancellation(Task task)
        {
            try
            {
                await task;
                Assert.Fail("Expected cancellation.");
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
