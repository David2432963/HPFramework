using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using HP.Framework.Assets;
using HP.Framework.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HP.Framework.Tests
{
    public sealed class AudioManagerPlayModeTests
    {
        private sealed class FakeAudioLease : IAssetLease<AudioClip>
        {
            private readonly FakeAudioLeaseProvider owner;

            public FakeAudioLease(FakeAudioLeaseProvider owner, string key, AudioClip asset)
            {
                this.owner = owner;
                Key = key;
                Asset = asset;
            }

            public string Key { get; }
            public AudioClip Asset { get; }
            public bool IsValid => Asset != null;

            public void Dispose()
            {
                owner.ReleaseCount++;
            }
        }

        private sealed class FakeAudioLeaseProvider : IAssetLeaseProvider
        {
            public AudioClip Clip { get; set; }
            public int AcquireCount { get; private set; }
            public int ReleaseCount { get; set; }

            public UniTask<IAssetLease<T>> AcquireAsync<T>(
                string key,
                CancellationToken cancellationToken = default)
                where T : Object
            {
                cancellationToken.ThrowIfCancellationRequested();
                AcquireCount++;
                IAssetLease<T> lease = new FakeAudioLease(this, key, Clip) as IAssetLease<T>;
                return UniTask.FromResult(lease);
            }

            public UniTask<IInstanceLease<T>> InstantiateLeaseAsync<T>(
                string key,
                Transform parent = null,
                CancellationToken cancellationToken = default)
                where T : Component
            {
                return UniTask.FromResult<IInstanceLease<T>>(null);
            }

            public UniTask<T> LoadAssetAsync<T>(
                string key,
                CancellationToken cancellationToken = default)
                where T : Object
            {
                return UniTask.FromResult(Clip as T);
            }

            public UniTask<GameObject> InstantiateAsync(
                string key,
                Transform parent = null,
                CancellationToken cancellationToken = default)
            {
                return UniTask.FromResult<GameObject>(null);
            }

            public UniTask<T> InstantiateAsync<T>(
                string key,
                Transform parent = null,
                CancellationToken cancellationToken = default)
                where T : Component
            {
                return UniTask.FromResult<T>(null);
            }

            public void ReleaseAsset<T>(T asset) where T : Object
            {
            }

            public void ReleaseInstance(GameObject instance)
            {
            }
        }

        private readonly List<Object> ownedObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = ownedObjects.Count - 1; i >= 0; i--)
            {
                if (ownedObjects[i] != null)
                {
                    Object.DestroyImmediate(ownedObjects[i]);
                }
            }

            ownedObjects.Clear();
        }

        [UnityTest]
        public IEnumerator HardBudget_StealsEligibleVoiceAndDropsLowerPriorityRequest()
        {
            AudioClip lowClip = CreateClip("Low");
            AudioClip highClip = CreateClip("High");
            AudioLibrarySO library = CreateLibrary(
                Entry("low", lowClip, priority: 0),
                Entry("high", highClip, priority: 10));
            AudioManager manager = CreateManager(library, 1, 1);

            manager.PlaySfx("low");
            manager.PlaySfx("high");
            manager.PlaySfx("low");

            AudioServiceStats stats = manager.Stats;
            Assert.That(stats.ActiveSfxVoices, Is.EqualTo(1));
            Assert.That(stats.MaxSfxVoices, Is.EqualTo(1));
            Assert.That(stats.PlayedTotal, Is.EqualTo(2));
            Assert.That(stats.StolenTotal, Is.EqualTo(1));
            Assert.That(stats.DroppedTotal, Is.EqualTo(1));

            manager.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PerKeyLimitAndRetriggerInterval_DropDuplicateRequest()
        {
            AudioClip clip = CreateClip("Limited");
            AudioLibrarySO library = CreateLibrary(new AudioLibrarySO.AudioEntry
            {
                key = "limited",
                clip = clip,
                assetMode = AudioAssetMode.DirectClip,
                maxSimultaneous = 1,
                minRetriggerInterval = 10f
            });
            AudioManager manager = CreateManager(library, 2, 2);

            manager.PlaySfx("limited");
            manager.PlaySfx("limited");

            Assert.That(manager.Stats.PlayedTotal, Is.EqualTo(1));
            Assert.That(manager.Stats.DroppedTotal, Is.EqualTo(1));

            manager.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReusedChannel_ResetsPositionAndSpatialBlend()
        {
            AudioClip clip = CreateClip("Spatial");
            AudioLibrarySO library = CreateLibrary(new AudioLibrarySO.AudioEntry
            {
                key = "spatial",
                clip = clip,
                assetMode = AudioAssetMode.DirectClip,
                spatialBlend = 0.25f
            });
            AudioManager manager = CreateManager(library, 1, 1);

            manager.PlaySfxAtPosition("spatial", new Vector3(4f, 5f, 6f));
            manager.StopAllSfx();
            manager.PlaySfx("spatial");

            AudioSource channel = FindSfxChannel(manager);
            Assert.That(channel.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(channel.spatialBlend, Is.EqualTo(0.25f).Within(0.001f));

            manager.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AssetBackedAudio_ReleasesVoiceAndReplacedMusicLeases()
        {
            AudioClip clip = CreateClip("Leased");
            AudioLibrarySO library = CreateLibrary(new AudioLibrarySO.AudioEntry
            {
                key = "leased",
                assetMode = AudioAssetMode.AssetKey,
                assetKey = "Audio/Leased"
            });
            var provider = new FakeAudioLeaseProvider { Clip = clip };
            AudioManager manager = CreateManager(library, 1, 1, provider);

            yield return manager.PlaySfxAsync("leased").ToCoroutine();
            Assert.That(provider.AcquireCount, Is.EqualTo(1));
            manager.StopAllSfx();
            Assert.That(provider.ReleaseCount, Is.EqualTo(1));

            yield return manager.PlayMusicAsync("leased", fadeDuration: 0f).ToCoroutine();
            yield return manager.PlayMusicAsync("leased", fadeDuration: 0f).ToCoroutine();
            Assert.That(provider.AcquireCount, Is.EqualTo(3));
            Assert.That(provider.ReleaseCount, Is.EqualTo(2));
            manager.StopMusic();
            Assert.That(provider.ReleaseCount, Is.EqualTo(3));

            manager.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AssetBackedSfx_ReleasesLeaseWhenChannelFinishesWithoutAnotherServiceCall()
        {
            AudioClip clip = CreateClip("LeasedCompletion");
            AudioLibrarySO library = CreateLibrary(new AudioLibrarySO.AudioEntry
            {
                key = "leased-completion",
                assetMode = AudioAssetMode.AssetKey,
                assetKey = "Audio/LeasedCompletion"
            });
            var provider = new FakeAudioLeaseProvider { Clip = clip };
            AudioManager manager = CreateManager(library, 1, 1, provider);

            yield return manager.PlaySfxAsync("leased-completion").ToCoroutine();
            AudioSource channel = FindSfxChannel(manager);
            channel.Stop();
            yield return null;

            Assert.That(provider.ReleaseCount, Is.EqualTo(1));
            manager.Dispose();
        }

        private AudioManager CreateManager(
            AudioLibrarySO library,
            int initialChannels,
            int maxChannels,
            IAssetLeaseProvider provider = null)
        {
            var root = new GameObject("AudioManager_Test");
            ownedObjects.Add(root);
            AudioManager manager = root.AddComponent<AudioManager>();
            SetField(manager, "sfxChannelCount", initialChannels);
            SetField(manager, "maxSfxChannelCount", maxChannels);
            manager.ConfigureLibrary(library);
            manager.Construct(null, provider);
            manager.Initialize();
            return manager;
        }

        private AudioLibrarySO CreateLibrary(params AudioLibrarySO.AudioEntry[] entries)
        {
            AudioLibrarySO library = ScriptableObject.CreateInstance<AudioLibrarySO>();
            ownedObjects.Add(library);
            typeof(AudioLibrarySO)
                .GetField("audioEntries", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(library, new List<AudioLibrarySO.AudioEntry>(entries));
            library.InitializeLookup();
            return library;
        }

        private AudioClip CreateClip(string name)
        {
            AudioClip clip = AudioClip.Create(name, 44100, 1, 44100, false);
            ownedObjects.Add(clip);
            return clip;
        }

        private static AudioLibrarySO.AudioEntry Entry(
            string key,
            AudioClip clip,
            int priority)
        {
            return new AudioLibrarySO.AudioEntry
            {
                key = key,
                clip = clip,
                assetMode = AudioAssetMode.DirectClip,
                priority = priority
            };
        }

        private static AudioSource FindSfxChannel(AudioManager manager)
        {
            AudioSource[] sources = manager.GetComponentsInChildren<AudioSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i].gameObject.name.StartsWith("SfxChannel"))
                {
                    return sources[i];
                }
            }

            Assert.Fail("No pooled SFX channel was created.");
            return null;
        }

        private static void SetField<T>(AudioManager manager, string name, T value)
        {
            typeof(AudioManager)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(manager, value);
        }
    }
}
