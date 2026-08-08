namespace HP.Framework.Audio
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using HP.Framework;
    using UnityEngine;
    using VContainer;
    using VContainer.Unity;
    using HP.Framework.Audio;
    using HP.Framework.Persistence;

    /// <summary>
    /// Global music and SFX service. Uses Unity coroutines for fades so the core package has no
    /// DOTween dependency, and mirrors all mutable values through ISettingsService.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour, IAudioService, IInitializable, IDisposable
    {
        [Serializable]
        public struct AudioSettingsData
        {
            public bool MusicMuted;
            public float MusicVolume;
            public bool SfxMuted;
            public float SfxVolume;
        }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Volume Settings")]
        [SerializeField] private bool musicMuted;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;
        [SerializeField] private bool sfxMuted;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        [Header("SFX Channel Pool")]
        [SerializeField, Min(1)] private int sfxChannelCount = 10;
        [SerializeField, Min(1)] private int maxSfxChannelCount = 30;

        [Header("Audio Database")]
        [SerializeField] private AudioLibrarySO audioLibrary;

        private readonly List<AudioSource> sfxChannels = new List<AudioSource>();
        private readonly List<float> sfxChannelVolumeScales = new List<float>();
        private readonly Dictionary<string, int> clusterSequentialIndices =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private ISettingsService settingsService;
        private Coroutine musicFadeRoutine;
        private float musicVolumeScale = 1f;
        private bool initialized;

        private AudioClip pausedMusicClip;
        private float pausedMusicTime;
        private float pausedMusicVolumeScale;
        private bool hasPausedMusic;

        public bool IsMusicPlaying => musicSource != null && musicSource.isPlaying;
        public bool IsMusicMuted => musicMuted;
        public bool IsSfxMuted => sfxMuted;

        [Inject]
        public void Construct(ISettingsService settingsService)
        {
            this.settingsService = settingsService;
        }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            maxSfxChannelCount = Mathf.Max(1, maxSfxChannelCount);
            sfxChannelCount = Mathf.Clamp(sfxChannelCount, 1, maxSfxChannelCount);
            EnsureAudioSources();
            audioLibrary?.InitializeLookup();
            InitializeSfxPool();

            if (settingsService != null)
            {
                settingsService.SettingChanged -= OnSettingChanged;
                settingsService.SettingChanged += OnSettingChanged;
                SyncWithSettings();
            }

            ApplySettings();
            initialized = true;
        }

        public void ConfigureLibrary(AudioLibrarySO library)
        {
            audioLibrary = library ?? throw new ArgumentNullException(nameof(library));
            audioLibrary.InitializeLookup();
        }

        public void PlayMusic(
            AudioClip clip,
            bool loop = true,
            float fadeDuration = 0.5f,
            float volumeScale = 1f,
            float startTime = 0f)
        {
            if (clip == null)
            {
                return;
            }

            EnsureInitialized();
            StopMusicFade();
            musicVolumeScale = Mathf.Max(0f, volumeScale);
            float targetVolume = GetTargetMusicVolume();
            float safeStartTime = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - 0.01f));

            if (fadeDuration > 0f && musicSource.isPlaying && musicSource.clip != clip)
            {
                musicFadeRoutine = StartCoroutine(CrossFadeMusic(
                    clip,
                    loop,
                    safeStartTime,
                    targetVolume,
                    fadeDuration));
                return;
            }

            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.time = safeStartTime;
            musicSource.volume = targetVolume;
            musicSource.Play();
        }

        public void PlayMusic(
            string key,
            bool loop = true,
            float fadeDuration = 0.5f,
            float volumeScale = 1f)
        {
            if (TryGetClip(key, out AudioClip clip))
            {
                PlayMusic(clip, loop, fadeDuration, volumeScale);
            }
        }

        public void OverrideMusic(
            AudioClip clip,
            bool loop = true,
            float fadeDuration = 0.5f,
            float volumeScale = 1f)
        {
            EnsureInitialized();
            if (musicSource.isPlaying && musicSource.clip != null)
            {
                pausedMusicClip = musicSource.clip;
                pausedMusicTime = musicSource.time;
                pausedMusicVolumeScale = musicVolumeScale;
                hasPausedMusic = true;
            }

            PlayMusic(clip, loop, fadeDuration, volumeScale);
        }

        public void RestoreMusic(float fadeDuration = 0.5f)
        {
            if (hasPausedMusic && pausedMusicClip != null)
            {
                AudioClip restoreClip = pausedMusicClip;
                float restoreTime = pausedMusicTime;
                float restoreScale = pausedMusicVolumeScale;
                ClearPausedMusic();
                PlayMusic(restoreClip, true, fadeDuration, restoreScale, restoreTime);
                return;
            }

            ClearPausedMusic();
            if (fadeDuration <= 0f || musicSource == null || !musicSource.isPlaying)
            {
                StopMusic();
                return;
            }

            StopMusicFade();
            musicFadeRoutine = StartCoroutine(FadeOutAndStop(fadeDuration));
        }

        public void PauseMusic()
        {
            musicSource?.Pause();
        }

        public void ResumeMusic()
        {
            if (musicSource != null && musicSource.clip != null && !musicSource.isPlaying)
            {
                musicSource.UnPause();
            }
        }

        public void StopMusic()
        {
            StopMusicFade();
            if (musicSource != null)
            {
                musicSource.Stop();
            }
        }

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            PlaySfxInternal(clip, null, volumeScale);
        }

        public void PlaySfx(string key, float volumeScale = 1f)
        {
            if (TryGetClip(key, out AudioClip clip))
            {
                PlaySfx(clip, volumeScale);
            }
        }

        public void PlayRandomSfxInCluster(string clusterId, float volumeScale = 1f)
        {
            PlaySfx(clusterId, volumeScale);
        }

        public void PlaySequentialSfxInCluster(string clusterId, float volumeScale = 1f)
        {
            if (TryGetSequentialClip(clusterId, out AudioClip clip))
            {
                PlaySfx(clip, volumeScale);
            }
        }

        public void PlaySfxAtPosition(
            AudioClip clip,
            Vector3 position,
            float volumeScale = 1f)
        {
            PlaySfxInternal(clip, position, volumeScale);
        }

        public void PlaySfxAtPosition(
            string key,
            Vector3 position,
            float volumeScale = 1f)
        {
            if (TryGetClip(key, out AudioClip clip))
            {
                PlaySfxAtPosition(clip, position, volumeScale);
            }
        }

        public void PlayRandomSfxInClusterAtPosition(
            string clusterId,
            Vector3 position,
            float volumeScale = 1f)
        {
            PlaySfxAtPosition(clusterId, position, volumeScale);
        }

        public void PlaySequentialSfxInClusterAtPosition(
            string clusterId,
            Vector3 position,
            float volumeScale = 1f)
        {
            if (TryGetSequentialClip(clusterId, out AudioClip clip))
            {
                PlaySfxAtPosition(clip, position, volumeScale);
            }
        }

        public void PauseSfx()
        {
            sfxSource?.Pause();
            for (int i = 0; i < sfxChannels.Count; i++)
            {
                sfxChannels[i]?.Pause();
            }
        }

        public void ResumeSfx()
        {
            if (sfxSource != null && sfxSource.clip != null)
            {
                sfxSource.UnPause();
            }

            for (int i = 0; i < sfxChannels.Count; i++)
            {
                AudioSource channel = sfxChannels[i];
                if (channel != null && channel.clip != null)
                {
                    channel.UnPause();
                }
            }
        }

        public void StopAllSfx()
        {
            sfxSource?.Stop();
            for (int i = 0; i < sfxChannels.Count; i++)
            {
                sfxChannels[i]?.Stop();
            }
        }

        public void StopAllAudio()
        {
            StopMusic();
            StopAllSfx();
        }

        public void SetMusicMuted(bool muted)
        {
            if (settingsService != null)
            {
                settingsService.MusicEnabled = !muted;
            }
            else
            {
                musicMuted = muted;
                ApplySettings();
            }
        }

        public void SetSfxMuted(bool muted)
        {
            if (settingsService != null)
            {
                settingsService.SoundEnabled = !muted;
            }
            else
            {
                sfxMuted = muted;
                ApplySettings();
            }
        }

        public void SetMusicVolume(float volume)
        {
            float clamped = Mathf.Clamp01(volume);
            if (settingsService != null)
            {
                settingsService.MusicVolume = clamped;
            }
            else
            {
                musicVolume = clamped;
                ApplySettings();
            }
        }

        public void SetSfxVolume(float volume)
        {
            float clamped = Mathf.Clamp01(volume);
            if (settingsService != null)
            {
                settingsService.SoundVolume = clamped;
            }
            else
            {
                sfxVolume = clamped;
                ApplySettings();
            }
        }

        public float GetMusicVolume() => musicVolume;
        public float GetSfxVolume() => sfxVolume;

        public AudioSettingsData GetSettingsData()
        {
            return new AudioSettingsData
            {
                MusicMuted = musicMuted,
                MusicVolume = musicVolume,
                SfxMuted = sfxMuted,
                SfxVolume = sfxVolume
            };
        }

        public void ApplySavedSettings(AudioSettingsData settings)
        {
            SetMusicMuted(settings.MusicMuted);
            SetMusicVolume(settings.MusicVolume);
            SetSfxMuted(settings.SfxMuted);
            SetSfxVolume(settings.SfxVolume);
        }

        public void Dispose()
        {
            if (settingsService != null)
            {
                settingsService.SettingChanged -= OnSettingChanged;
            }

            StopMusicFade();
            StopAllSfx();
        }

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                Initialize();
            }
        }

        private void EnsureAudioSources()
        {
            AudioSource[] existingSources = GetComponents<AudioSource>();
            if (musicSource == null)
            {
                musicSource = existingSources.Length > 0
                    ? existingSources[0]
                    : gameObject.AddComponent<AudioSource>();
            }

            if (sfxSource == null)
            {
                existingSources = GetComponents<AudioSource>();
                sfxSource = existingSources.Length > 1
                    ? existingSources[1]
                    : gameObject.AddComponent<AudioSource>();
            }

            musicSource.playOnAwake = false;
            musicSource.loop = true;
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }

        private void InitializeSfxPool()
        {
            if (sfxChannels.Count > 0)
            {
                return;
            }

            for (int i = 0; i < sfxChannelCount; i++)
            {
                CreateSfxChannel($"SfxChannel_{i}");
            }
        }

        private AudioSource CreateSfxChannel(string channelName)
        {
            GameObject channelObject = new GameObject(channelName);
            channelObject.transform.SetParent(transform, false);
            AudioSource source = channelObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.mute = sfxMuted;
            sfxChannels.Add(source);
            sfxChannelVolumeScales.Add(1f);
            return source;
        }

        private void PlaySfxInternal(
            AudioClip clip,
            Vector3? worldPosition,
            float volumeScale)
        {
            if (clip == null || sfxMuted)
            {
                return;
            }

            EnsureInitialized();
            AudioSource channel = GetFreeChannel(out int channelIndex);
            if (channel == null)
            {
                sfxSource.PlayOneShot(clip, Mathf.Clamp01(sfxVolume * volumeScale));
                return;
            }

            float safeScale = Mathf.Max(0f, volumeScale);
            sfxChannelVolumeScales[channelIndex] = safeScale;
            channel.transform.localPosition = Vector3.zero;
            channel.spatialBlend = worldPosition.HasValue ? 1f : 0f;
            if (worldPosition.HasValue)
            {
                channel.transform.position = worldPosition.Value;
            }

            channel.clip = clip;
            channel.volume = Mathf.Clamp01(sfxVolume * safeScale);
            channel.Play();
        }

        private AudioSource GetFreeChannel(out int index)
        {
            for (int i = 0; i < sfxChannels.Count; i++)
            {
                AudioSource channel = sfxChannels[i];
                if (channel != null && !channel.isPlaying)
                {
                    index = i;
                    return channel;
                }
            }

            if (sfxChannels.Count < maxSfxChannelCount)
            {
                index = sfxChannels.Count;
                return CreateSfxChannel($"SfxChannel_Dynamic_{index}");
            }

            index = -1;
            return null;
        }

        private bool TryGetClip(string key, out AudioClip clip)
        {
            clip = null;
            if (audioLibrary != null && audioLibrary.TryGetClip(key, out clip))
            {
                return true;
            }

            BaseLog.LogWarning($"Audio key '{key}' was not found in AudioLibrary.");
            return false;
        }

        private bool TryGetSequentialClip(string key, out AudioClip clip)
        {
            clip = null;
            if (audioLibrary == null)
            {
                BaseLog.LogWarning("AudioLibrary is not configured.");
                return false;
            }

            int index = clusterSequentialIndices.TryGetValue(key, out int savedIndex)
                ? savedIndex
                : -1;
            if (!audioLibrary.TryGetSequentialClip(key, ref index, out clip))
            {
                BaseLog.LogWarning($"Audio cluster '{key}' was not found in AudioLibrary.");
                return false;
            }

            clusterSequentialIndices[key] = index;
            return true;
        }

        private void SyncWithSettings()
        {
            if (settingsService == null)
            {
                return;
            }

            musicMuted = !settingsService.MusicEnabled;
            musicVolume = settingsService.MusicVolume;
            sfxMuted = !settingsService.SoundEnabled;
            sfxVolume = settingsService.SoundVolume;
        }

        private void OnSettingChanged(string settingName)
        {
            if (settingName == nameof(ISettingsProvider.MusicEnabled)
                || settingName == nameof(ISettingsProvider.MusicVolume)
                || settingName == nameof(ISettingsProvider.SoundEnabled)
                || settingName == nameof(ISettingsProvider.SoundVolume))
            {
                SyncWithSettings();
                ApplySettings();
            }
        }

        private void ApplySettings()
        {
            EnsureAudioSources();
            musicSource.mute = musicMuted;
            musicSource.volume = GetTargetMusicVolume();
            sfxSource.mute = sfxMuted;
            sfxSource.volume = sfxMuted ? 0f : sfxVolume;

            for (int i = 0; i < sfxChannels.Count; i++)
            {
                AudioSource channel = sfxChannels[i];
                if (channel == null)
                {
                    continue;
                }

                channel.mute = sfxMuted;
                channel.volume = sfxMuted
                    ? 0f
                    : Mathf.Clamp01(sfxVolume * sfxChannelVolumeScales[i]);
            }
        }

        private float GetTargetMusicVolume()
        {
            return musicMuted ? 0f : Mathf.Clamp01(musicVolume * musicVolumeScale);
        }

        private IEnumerator CrossFadeMusic(
            AudioClip nextClip,
            bool loop,
            float startTime,
            float targetVolume,
            float duration)
        {
            float halfDuration = Mathf.Max(0.01f, duration * 0.5f);
            yield return FadeVolume(musicSource, musicSource.volume, 0f, halfDuration);

            musicSource.Stop();
            musicSource.clip = nextClip;
            musicSource.loop = loop;
            musicSource.time = startTime;
            musicSource.volume = 0f;
            musicSource.Play();

            yield return FadeVolume(musicSource, 0f, targetVolume, halfDuration);
            musicFadeRoutine = null;
        }

        private IEnumerator FadeOutAndStop(float duration)
        {
            yield return FadeVolume(musicSource, musicSource.volume, 0f, duration);
            musicSource.Stop();
            musicFadeRoutine = null;
        }

        private static IEnumerator FadeVolume(
            AudioSource source,
            float from,
            float to,
            float duration)
        {
            if (source == null)
            {
                yield break;
            }

            if (duration <= 0f)
            {
                source.volume = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            source.volume = to;
        }

        private void StopMusicFade()
        {
            if (musicFadeRoutine == null)
            {
                return;
            }

            StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = null;
        }

        private void ClearPausedMusic()
        {
            hasPausedMusic = false;
            pausedMusicClip = null;
            pausedMusicTime = 0f;
            pausedMusicVolumeScale = 1f;
        }
    }


}


