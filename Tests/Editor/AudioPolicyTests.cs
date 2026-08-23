using System.Collections.Generic;
using System.Reflection;
using HP.Framework.Audio;
using NUnit.Framework;
using UnityEngine;

namespace HP.Framework.Tests.Editor
{
    public sealed class AudioPolicyTests
    {
        [Test]
        public void VoiceSelector_StealsLowestPriorityThenOldestEligibleVoice()
        {
            var voices = new List<AudioVoiceCandidate>
            {
                new AudioVoiceCandidate(true, 2, 20),
                new AudioVoiceCandidate(true, 1, 30),
                new AudioVoiceCandidate(true, 1, 10),
                new AudioVoiceCandidate(true, 5, 1)
            };

            int selected = AudioVoiceSelector.SelectStealCandidate(voices, 3);

            Assert.That(selected, Is.EqualTo(2));
        }

        [Test]
        public void VoiceSelector_DropsWhenEveryActiveVoiceHasHigherPriority()
        {
            var voices = new List<AudioVoiceCandidate>
            {
                new AudioVoiceCandidate(true, 5, 1),
                new AudioVoiceCandidate(true, 10, 2)
            };

            Assert.That(AudioVoiceSelector.SelectStealCandidate(voices, 4), Is.EqualTo(-1));
        }

        [Test]
        public void PlaybackPolicy_ClampsBudgetAndSpatialValues()
        {
            var policy = new AudioPlaybackPolicy(
                3,
                -2,
                -1f,
                2f,
                AudioCategory.UI);

            Assert.That(policy.Priority, Is.EqualTo(3));
            Assert.That(policy.MaxSimultaneous, Is.Zero);
            Assert.That(policy.MinRetriggerInterval, Is.Zero);
            Assert.That(policy.SpatialBlend, Is.EqualTo(1f));
            Assert.That(policy.Category, Is.EqualTo(AudioCategory.UI));
        }

        [Test]
        public void AudioLibrary_SupportsDirectClipAndAssetKeyEntries()
        {
            AudioLibrarySO library = ScriptableObject.CreateInstance<AudioLibrarySO>();
            AudioClip clip = AudioClip.Create("Direct", 256, 1, 8000, false);
            try
            {
                SetEntries(library, new List<AudioLibrarySO.AudioEntry>
                {
                    new AudioLibrarySO.AudioEntry
                    {
                        key = "direct",
                        clip = clip,
                        assetMode = AudioAssetMode.DirectClip
                    },
                    new AudioLibrarySO.AudioEntry
                    {
                        key = "streamed",
                        assetMode = AudioAssetMode.AssetKey,
                        assetKey = "Audio/Streamed"
                    }
                });
                library.InitializeLookup();

                Assert.That(library.TryValidate(out string error), Is.True, error);
                Assert.That(library.TryGetClip("direct", out AudioClip direct), Is.True);
                Assert.That(direct, Is.SameAs(clip));
                Assert.That(library.TryGetClip("streamed", out _), Is.False);
                Assert.That(library.TryGetEntry("streamed", out AudioLibrarySO.AudioEntry entry), Is.True);
                Assert.That(entry.assetKey, Is.EqualTo("Audio/Streamed"));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(library);
            }
        }

        private static void SetEntries(
            AudioLibrarySO library,
            List<AudioLibrarySO.AudioEntry> entries)
        {
            typeof(AudioLibrarySO)
                .GetField("audioEntries", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(library, entries);
        }
    }
}
