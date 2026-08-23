using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using HP.Framework.Assets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HP.Framework.Tests
{
    public sealed class ResourcesAssetProviderPlayModeTests
    {
        private sealed class FakeLoader : IResourcesAssetLoader
        {
            private readonly Object asset;

            public FakeLoader(Object asset)
            {
                this.asset = asset;
            }

            public int LoadCount { get; private set; }
            public int UnloadCount { get; private set; }

            public UniTask<Object> LoadAsync(string key, Type requestedType)
            {
                LoadCount++;
                return UniTask.FromResult(asset);
            }

            public void Unload(Object value)
            {
                UnloadCount++;
            }
        }

        [UnityTest]
        public IEnumerator InstanceLease_DoubleDispose_DestroysExactlyOnceAndReleasesAssetOwnership()
        {
            GameObject prefab = new GameObject("LeasePrefab");
            var loader = new FakeLoader(prefab);
            var provider = new ResourcesAssetProvider(loader, null);
            IInstanceLease<Transform> lease = null;

            yield return provider.InstantiateLeaseAsync<Transform>("prefab")
                .ToCoroutine(result => lease = result);

            Assert.That(lease, Is.Not.Null);
            Assert.That(lease.IsValid, Is.True);
            GameObject instance = lease.GameObject;
            Assert.That(provider.GetStats().TrackedInstanceCount, Is.EqualTo(1));
            Assert.That(provider.GetStats().ActiveReferenceCount, Is.EqualTo(1));

            lease.Dispose();
            lease.Dispose();
            Assert.That(provider.GetStats().TrackedInstanceCount, Is.EqualTo(0));
            Assert.That(provider.GetStats().ActiveReferenceCount, Is.EqualTo(0));

            yield return null;
            Assert.That(instance == null, Is.True);

            provider.TrimUnused();
            Assert.That(loader.LoadCount, Is.EqualTo(1));
            Assert.That(provider.LoadedAssetCount, Is.EqualTo(0));
            Assert.That(loader.UnloadCount, Is.EqualTo(0),
                "GameObject assets are removed from provider ownership without Resources.UnloadAsset.");

            provider.Dispose();
            Object.Destroy(prefab);
            yield return null;
        }
    }
}
