using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using HP.Framework.Assets;
using HP.Framework.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HP.Framework.Tests
{
    public sealed class AssetPopupProbe : BasePopup
    {
    }

    public sealed class AssetScreenProbe : BaseScreen
    {
    }

    public sealed class AsyncUIAssetPolicyPlayModeTests
    {
        private sealed class FakeLease : IAssetLease<GameObject>
        {
            private readonly FakeProvider owner;
            private bool disposed;

            public FakeLease(FakeProvider owner, string key, GameObject asset)
            {
                this.owner = owner;
                Key = key;
                Asset = asset;
            }

            public string Key { get; }
            public GameObject Asset { get; }
            public bool IsValid => !disposed && Asset != null;

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                owner.ReleaseCount++;
            }
        }

        private sealed class FakeProvider : IAssetLeaseProvider
        {
            private UniTaskCompletionSource<IAssetLease<GameObject>> deferred;

            public GameObject Asset { get; set; }
            public bool IsDeferred { get; set; }
            public int AcquireCount { get; private set; }
            public int ReleaseCount { get; set; }

            public async UniTask<IAssetLease<T>> AcquireAsync<T>(
                string key,
                CancellationToken cancellationToken = default)
                where T : UnityEngine.Object
            {
                AcquireCount++;
                IAssetLease<GameObject> lease;
                if (IsDeferred)
                {
                    deferred ??= new UniTaskCompletionSource<IAssetLease<GameObject>>();
                    lease = await deferred.Task;
                }
                else
                {
                    lease = new FakeLease(this, key, Asset);
                }

                return (IAssetLease<T>)(object)lease;
            }

            public void Complete(string key = "UI/Test")
            {
                deferred?.TrySetResult(new FakeLease(this, key, Asset));
            }

            public UniTask<IInstanceLease<T>> InstantiateLeaseAsync<T>(
                string key,
                Transform parent = null,
                CancellationToken cancellationToken = default)
                where T : Component => UniTask.FromResult<IInstanceLease<T>>(null);

            public UniTask<T> LoadAssetAsync<T>(
                string key,
                CancellationToken cancellationToken = default)
                where T : UnityEngine.Object => UniTask.FromResult(Asset as T);

            public UniTask<GameObject> InstantiateAsync(
                string key,
                Transform parent = null,
                CancellationToken cancellationToken = default) =>
                UniTask.FromResult<GameObject>(null);

            public UniTask<T> InstantiateAsync<T>(
                string key,
                Transform parent = null,
                CancellationToken cancellationToken = default)
                where T : Component => UniTask.FromResult<T>(null);

            public void ReleaseAsset<T>(T asset) where T : UnityEngine.Object
            {
            }

            public void ReleaseInstance(GameObject instance)
            {
            }
        }

        private readonly List<UnityEngine.Object> ownedObjects =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = ownedObjects.Count - 1; i >= 0; i--)
            {
                if (ownedObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownedObjects[i]);
                }
            }

            ownedObjects.Clear();
        }

        [UnityTest]
        public IEnumerator DirectMode_RemainsSynchronous()
        {
            GameObject prefab = CreatePopupPrefab();
            UICatalogSO catalog = CreateCatalog(
                popupEntries: new[] { CreateEntry(prefab, null, typeof(AssetPopupProbe), true) });
            UIManager manager = CreateManager(catalog, null);

            AssetPopupProbe popup = manager.OpenPopup<AssetPopupProbe>();

            Assert.That(popup, Is.Not.Null);
            Assert.That(popup.gameObject, Is.Not.SameAs(prefab));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SyncApi_ForAssetKeyEntry_FailsDescriptively()
        {
            GameObject prefab = CreatePopupPrefab();
            var provider = new FakeProvider { Asset = prefab };
            UICatalogSO catalog = CreateCatalog(
                popupEntries: new[] { CreateEntry(null, "UI/Popup", typeof(AssetPopupProbe), true) });
            UIManager manager = CreateManager(catalog, provider);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => manager.OpenPopup<AssetPopupProbe>());

            Assert.That(exception.Message, Does.Contain("OpenPopupAsync"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConcurrentOpen_LoadsAndCreatesOnlyOnce()
        {
            GameObject prefab = CreatePopupPrefab();
            var provider = new FakeProvider { Asset = prefab, IsDeferred = true };
            UIManager manager = CreateManager(
                CreateCatalog(popupEntries: new[]
                {
                    CreateEntry(null, "UI/Popup", typeof(AssetPopupProbe), true)
                }),
                provider);

            UniTask<AssetPopupProbe> firstTask = manager.OpenPopupAsync<AssetPopupProbe>();
            UniTask<AssetPopupProbe> secondTask = manager.OpenPopupAsync<AssetPopupProbe>();
            provider.Complete("UI/Popup");
            AssetPopupProbe first = null;
            AssetPopupProbe second = null;
            yield return firstTask.ToCoroutine(result => first = result);
            yield return secondTask.ToCoroutine(result => second = result);

            Assert.That(provider.AcquireCount, Is.EqualTo(1));
            Assert.That(first, Is.SameAs(second));
        }

        [UnityTest]
        public IEnumerator OneCancelledWaiter_DoesNotCancelSharedCreation()
        {
            GameObject prefab = CreatePopupPrefab();
            var provider = new FakeProvider { Asset = prefab, IsDeferred = true };
            UIManager manager = CreateManager(
                CreateCatalog(popupEntries: new[]
                {
                    CreateEntry(null, "UI/Popup", typeof(AssetPopupProbe), true)
                }),
                provider);
            using var cancellation = new CancellationTokenSource();

            UniTask<(bool IsCanceled, AssetPopupProbe Result)> cancelledTask = manager
                .OpenPopupAsync<AssetPopupProbe>(cancellation.Token)
                .SuppressCancellationThrow();
            UniTask<AssetPopupProbe> survivorTask = manager.OpenPopupAsync<AssetPopupProbe>();
            cancellation.Cancel();
            provider.Complete("UI/Popup");
            bool cancelled = false;
            AssetPopupProbe survivor = null;
            yield return cancelledTask.ToCoroutine(result => cancelled = result.IsCanceled);
            yield return survivorTask.ToCoroutine(result => survivor = result);

            Assert.That(cancelled, Is.True);
            Assert.That(survivor, Is.Not.Null);
            Assert.That(provider.AcquireCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AllWaitersCancelled_CleansLateInstanceLeaseAndPendingRecord()
        {
            GameObject prefab = CreatePopupPrefab();
            var provider = new FakeProvider { Asset = prefab, IsDeferred = true };
            UIManager manager = CreateManager(
                CreateCatalog(popupEntries: new[]
                {
                    CreateEntry(null, "UI/Popup", typeof(AssetPopupProbe), true)
                }),
                provider);
            using var firstCancellation = new CancellationTokenSource();
            using var secondCancellation = new CancellationTokenSource();
            UniTask<(bool IsCanceled, AssetPopupProbe Result)> first = manager
                .OpenPopupAsync<AssetPopupProbe>(firstCancellation.Token)
                .SuppressCancellationThrow();
            UniTask<(bool IsCanceled, AssetPopupProbe Result)> second = manager
                .OpenPopupAsync<AssetPopupProbe>(secondCancellation.Token)
                .SuppressCancellationThrow();

            firstCancellation.Cancel();
            secondCancellation.Cancel();
            yield return first.ToCoroutine();
            yield return second.ToCoroutine();
            provider.Complete("UI/Popup");
            yield return null;

            Assert.That(provider.ReleaseCount, Is.EqualTo(1));
            Assert.That(manager.GetPopup<AssetPopupProbe>(), Is.Null);
            Assert.That(GetPendingCount(manager), Is.Zero);
        }

        [UnityTest]
        public IEnumerator CachePolicy_RetainsUntilClear_AndDestroyPolicyReleasesOnHide()
        {
            GameObject prefab = CreatePopupPrefab();
            var provider = new FakeProvider { Asset = prefab };
            UIManager manager = CreateManager(
                CreateCatalog(popupEntries: new[]
                {
                    CreateEntry(null, "UI/Popup", typeof(AssetPopupProbe), true)
                }),
                provider);
            AssetPopupProbe cached = null;
            yield return manager.OpenPopupAsync<AssetPopupProbe>()
                .ToCoroutine(result => cached = result);

            manager.NotifyPopupHidden(cached);
            Assert.That(provider.ReleaseCount, Is.Zero);
            manager.ClearPopups();
            Assert.That(provider.ReleaseCount, Is.EqualTo(1));

            manager.ConfigureCatalog(CreateCatalog(popupEntries: new[]
            {
                CreateEntry(null, "UI/Popup", typeof(AssetPopupProbe), false)
            }));
            AssetPopupProbe transient = null;
            yield return manager.OpenPopupAsync<AssetPopupProbe>()
                .ToCoroutine(result => transient = result);
            manager.NotifyPopupHidden(transient);

            Assert.That(provider.ReleaseCount, Is.EqualTo(2));
            Assert.That(manager.GetPopup<AssetPopupProbe>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator AssetBackedScreen_ReleasesOnDestroyAfterHide()
        {
            GameObject prefab = CreateScreenPrefab();
            var provider = new FakeProvider { Asset = prefab };
            UIManager manager = CreateManager(
                CreateCatalog(screenEntries: new[]
                {
                    CreateEntry(null, "UI/Screen", typeof(AssetScreenProbe), false)
                }),
                provider);

            AssetScreenProbe screen = null;
            yield return manager.ShowScreenAsync<AssetScreenProbe>()
                .ToCoroutine(result => screen = result);
            Assert.That(screen, Is.Not.Null);
            manager.HideScreen<AssetScreenProbe>();

            Assert.That(provider.ReleaseCount, Is.EqualTo(1));
            Assert.That(manager.GetScreen<AssetScreenProbe>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator CatalogReconfigure_ReleasesCachedAssetLease()
        {
            GameObject prefab = CreatePopupPrefab();
            var provider = new FakeProvider { Asset = prefab };
            UIManager manager = CreateManager(
                CreateCatalog(popupEntries: new[]
                {
                    CreateEntry(null, "UI/Popup", typeof(AssetPopupProbe), true)
                }),
                provider);

            yield return manager.OpenPopupAsync<AssetPopupProbe>().ToCoroutine();
            Assert.That(provider.ReleaseCount, Is.Zero);

            manager.ConfigureCatalog(CreateCatalog());

            Assert.That(provider.ReleaseCount, Is.EqualTo(1));
            Assert.That(manager.GetPopup<AssetPopupProbe>(), Is.Null);
        }

        private UIManager CreateManager(UICatalogSO catalog, IAssetLeaseProvider provider)
        {
            var owner = new GameObject("UIManager_Test");
            ownedObjects.Add(owner);
            UIManager manager = owner.AddComponent<UIManager>();
            manager.Construct(null, null, provider);
            manager.ConfigureCatalog(catalog);
            return manager;
        }

        private UICatalogSO CreateCatalog(
            UICatalogSO.UIEntry[] popupEntries = null,
            UICatalogSO.UIEntry[] screenEntries = null)
        {
            UICatalogSO catalog = ScriptableObject.CreateInstance<UICatalogSO>();
            ownedObjects.Add(catalog);
            SetField(catalog, "popupEntries", new List<UICatalogSO.UIEntry>(
                popupEntries ?? Array.Empty<UICatalogSO.UIEntry>()));
            SetField(catalog, "screenEntries", new List<UICatalogSO.UIEntry>(
                screenEntries ?? Array.Empty<UICatalogSO.UIEntry>()));
            Assert.That(catalog.TryValidate(out string error), Is.True, error);
            return catalog;
        }

        private static UICatalogSO.UIEntry CreateEntry(
            GameObject prefab,
            string assetKey,
            Type runtimeType,
            bool cacheAfterClose)
        {
            var entry = new UICatalogSO.UIEntry();
            SetField(entry, "assetMode", prefab != null
                ? UIAssetMode.DirectPrefab
                : UIAssetMode.AssetKey);
            SetField(entry, "prefab", prefab);
            SetField(entry, "assetKey", assetKey);
            SetField(entry, "runtimeTypeName", runtimeType.AssemblyQualifiedName);
            SetField(entry, "cacheAfterClose", cacheAfterClose);
            return entry;
        }

        private GameObject CreatePopupPrefab()
        {
            var prefab = new GameObject("AssetPopupPrefab");
            prefab.AddComponent<AssetPopupProbe>();
            ownedObjects.Add(prefab);
            return prefab;
        }

        private GameObject CreateScreenPrefab()
        {
            var prefab = new GameObject("AssetScreenPrefab");
            prefab.AddComponent<AssetScreenProbe>();
            ownedObjects.Add(prefab);
            return prefab;
        }

        private static int GetPendingCount(UIManager manager)
        {
            object pending = typeof(UIManager)
                .GetField("pendingCreations", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(manager);
            return (int)pending.GetType().GetProperty("Count")?.GetValue(pending);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }
    }
}
