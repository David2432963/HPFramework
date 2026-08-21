using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using HP.Framework.Assets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HP.Framework.Tests
{
    public sealed class AssetOwnershipPlayModeTests
    {
        private sealed class DestroyCounter : MonoBehaviour
        {
            public static int DestroyCount;
            public bool CountDestroy;

            private void OnDestroy()
            {
                if (CountDestroy)
                {
                    DestroyCount++;
                }
            }
        }

        private sealed class FakeResourcesAssetLoader : IResourcesAssetLoader
        {
            public UnityEngine.Object Asset { get; set; }
            public int LoadCount { get; private set; }
            public int UnloadCount { get; private set; }

            public UniTask<UnityEngine.Object> LoadAsync(string key, Type requestedType)
            {
                LoadCount++;
                return UniTask.FromResult(Asset);
            }

            public void Unload(UnityEngine.Object asset)
            {
                UnloadCount++;
            }
        }

        [UnityTest]
        public IEnumerator InstanceLease_DisposeTwice_DestroysExactlyOnce_AndReleasesBackingLease()
        {
            DestroyCounter.DestroyCount = 0;
            GameObject prefab = new GameObject("AssetOwnershipPrefab");
            prefab.AddComponent<DestroyCounter>();
            var loader = new FakeResourcesAssetLoader { Asset = prefab };
            var provider = new ResourcesAssetProvider(loader);
            IInstanceLease<DestroyCounter> lease = null;

            yield return provider
                .InstantiateLeaseAsync<DestroyCounter>("prefab")
                .ToCoroutine(result => lease = result);

            Assert.That(lease, Is.Not.Null);
            Assert.That(lease.IsValid, Is.True);
            Assert.That(loader.LoadCount, Is.EqualTo(1));
            Assert.That(provider.GetStats().TrackedInstanceCount, Is.EqualTo(1));
            Assert.That(provider.GetStats().ActiveReferenceCount, Is.EqualTo(1));

            GameObject spawned = lease.GameObject;
            lease.Instance.CountDestroy = true;
            lease.Dispose();
            lease.Dispose();

            Assert.That(provider.GetStats().TrackedInstanceCount, Is.Zero);
            Assert.That(provider.GetStats().ActiveReferenceCount, Is.Zero);
            Assert.That(provider.UnusedAssetCount, Is.EqualTo(1));
            yield return null;

            Assert.That(spawned == null, Is.True);
            Assert.That(DestroyCounter.DestroyCount, Is.EqualTo(1));

            provider.TrimUnused();
            Assert.That(provider.LoadedAssetCount, Is.Zero);
            // GameObject/Component Resources have no direct Resources.UnloadAsset semantics.
            Assert.That(loader.UnloadCount, Is.Zero);

            provider.Dispose();
            yield return null;
            Assert.That(DestroyCounter.DestroyCount, Is.EqualTo(1));

            UnityEngine.Object.Destroy(prefab);
            yield return null;
        }
    }
}
