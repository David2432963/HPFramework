namespace HP.Framework.Audio
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using HP.Framework;
    using HP.Framework.Assets;
    using UnityEngine;
    using VContainer;
    using VContainer.Unity;
    using HP.Framework.Audio;
    using HP.Framework.Persistence;

    /// <summary>
    /// Global music and SFX service. Uses Unity coroutines for fades so the core package has no
    /// DOTween dependency, and mirrors all mutable values through ISettingsService.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour, IAudioService, IAudioDiagnostics, IInitializable, IDisposable
    {
        private sealed class SfxVoiceState
        {
            public bool Active;
            public string Key;
            public int Priority;
            public long Sequence;
            public float VolumeScale = 1f;
            public IAssetLease<AudioClip> Lease;
        }

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
        private readonly List<SfxVoiceState> sfxVoiceStates = new List<SfxVoiceState>();
        private readonly Dictionary<string, int> clusterSequentialIndices =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> lastSfxPlayTimes =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private readonly List<AudioVoiceCandidate> voiceCandidates = new List<AudioVoiceCandidate>();

        private ISettingsService settingsService;
        private IAssetLeaseProvider assetLeaseProvider;
        private Coroutine musicFadeRoutine;
        private IAssetLease<AudioClip> musicLease;
        private IAssetLease<AudioClip> pendingMusicLeaseRelease;
        private float musicVolumeScale = 1f;
        private bool initialized;
        private bool sfxPaused;
        private long nextVoiceSequence;
        private int playedSfxTotal;
        private int droppedSfxTotal;
        private int stolenSfxTotal;

        private AudioClip pausedMusicClip;
        private float pausedMusicTime;
        private float pausedMusicVolumeScale;
        private IAssetLease<AudioClip> pausedMusicLease;
        private bool hasPausedMusic;

        public bool IsMusicPlaying => musicSource != null && musicSource.isPlaying;
        public bool IsMusicMuted => musicMuted;
        public bool IsSfxMuted => sfxMuted;
        public AudioServiceStats Stats
        {
            get
            {
                EnsureInitialized();
                ReleaseCompletedSfxVoices();
                int activeCount = 0;
                for (int i = 0; i < sfxVoiceStates.Count; i++)
                {
                    if (sfxVoiceStates[i].Active)
                    {
                        activeCount++;
                    }
                }

                return new AudioServiceStats(
                    activeCount,
                    maxSfxChannelCount,
                    playedSfxTotal,
                    droppedSfxTotal,
                    stolenSfxTotal);
            }
        }

        [Inject]
        public void Construct(
            ISettingsService settingsService,
            IAssetLeaseProvider assetLeaseProvider)
        {
            this.settingsService = settingsService;
            this.assetLeaseProvider = assetLeaseProvider;
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
            PlayMusicInternal(clip, loop, fadeDuration, volumeScale, startTime, null);
        }

        public void PlayMusic(
            string key,
            bool loop = true,
            float fadeDuration = 0.5f,
            float volumeScale = 1f)
        {
            if (TryGetDirectEntry(key, out AudioLibrarySO.AudioEntry entry))
            {
                if (entry.assetMode == AudioAssetMode.DirectClip)
                {
                    PlayMusic(entry.clip, loop, fadeDuration, volumeScale);
                }
                else
                {
                    PlayMusicFromKeyAsync(key, loop, fadeDuration, volumeScale).Forget();
                }

                return;
            }

            if (TryGetClip(key, out AudioClip clip))
            {
                PlayMusic(clip, loop, fadeDuration, volumeScale);
            }
        }

        public async UniTask PlayMusicAsync(
            string key,
            bool loop = true,
            float fadeDuration = 0.5f,
            float volumeScale = 1f,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetDirectEntry(key, out AudioLibrarySO.AudioEntry entry))
            {
                if (TryGetClip(key, out AudioClip directClip))
                {
                    PlayMusic(directClip, loop, fadeDuration, volumeScale);
                }

                return;
            }

            if (entry.assetMode == AudioAssetMode.DirectClip)
            {
                PlayMusic(entry.clip, loop, fadeDuration, volumeScale);
                return;
            }

            IAssetLease<AudioClip> lease = await AcquireAudioClipAsync(
                entry.assetKey,
                cancellationToken);
            if (lease == null)
            {
                return;
            }

            PlayMusicInternal(
                lease.Asset,
                loop,
                fadeDuration,
                volumeScale,
                0f,
                lease);
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
                pausedMusicLease?.Dispose();
                pausedMusicLease = musicLease;
                musicLease = null;
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
                IAssetLease<AudioClip> restoreLease = pausedMusicLease;
                ClearPausedMusic(false);
                PlayMusicInternal(
                    restoreClip,
                    true,
                    fadeDuration,
                    restoreScale,
                    restoreTime,
                    restoreLease);
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

            ReleaseMusicLease();
        }

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            string voiceKey = clip != null ? $"clip:{clip.GetInstanceID()}" : string.Empty;
            PlaySfxInternal(
                clip,
                voiceKey,
                null,
                volumeScale,
                AudioPlaybackPolicy.Default,
                null);
        }

        public void PlaySfx(string key, float volumeScale = 1f)
        {
            if (TryGetDirectEntry(key, out AudioLibrarySO.AudioEntry entry))
            {
                if (entry.assetMode == AudioAssetMode.DirectClip)
                {
                    PlaySfxInternal(
                        entry.clip,
                        key,
                        null,
                        volumeScale,
                        entry.Policy,
                        null);
                }
                else
                {
                    PlaySfxFromKeyAsync(key, volumeScale, null).Forget();
                }

                return;
            }

            if (TryGetClip(key, out AudioClip clip))
            {
                PlaySfxInternal(
                    clip,
                    key,
                    null,
                    volumeScale,
                    AudioPlaybackPolicy.Default,
                    null);
            }
        }

        public async UniTask PlaySfxAsync(
            string key,
            float volumeScale = 1f,
            CancellationToken cancellationToken = default)
        {
            await PlaySfxFromKeyAsync(key, volumeScale, null, cancellationToken);
        }

        public void PlayRandomSfxInCluster(string clusterId, float volumeScale = 1f)
        {
            if (TryGetClusterClip(clusterId, out AudioClip clip, out AudioPlaybackPolicy policy))
            {
                PlaySfxInternal(clip, clusterId, null, volumeScale, policy, null);
            }
        }

        public void PlaySequentialSfxInCluster(string clusterId, float volumeScale = 1f)
        {
            if (TryGetSequentialClip(clusterId, out AudioClip clip))
            {
                PlaySfxInternal(
                    clip,
                    clusterId,
                    null,
                    volumeScale,
                    GetClusterPolicy(clusterId),
                    null);
            }
        }

        public void PlaySfxAtPosition(
            AudioClip clip,
            Vector3 position,
            float volumeScale = 1f)
        {
            string voiceKey = clip != null ? $"clip:{clip.GetInstanceID()}" : string.Empty;
            PlaySfxInternal(
                clip,
                voiceKey,
                position,
                volumeScale,
                AudioPlaybackPolicy.Default,
                null);
        }

        public void PlaySfxAtPosition(
            string key,
            Vector3 position,
            float volumeScale = 1f)
        {
            if (TryGetDirectEntry(key, out AudioLibrarySO.AudioEntry entry))
            {
                if (entry.assetMode == AudioAssetMode.DirectClip)
                {
                    PlaySfxInternal(
                        entry.clip,
                        key,
                        position,
                        volumeScale,
                        entry.Policy,
                        null);
                }
                else
                {
                    PlaySfxFromKeyAsync(key, volumeScale, position).Forget();
                }

                return;
            }

            if (TryGetClip(key, out AudioClip clip))
            {
                PlaySfxInternal(
                    clip,
                    key,
                    position,
                    volumeScale,
                    AudioPlaybackPolicy.Default,
                    null);
            }
        }

        public void PlayRandomSfxInClusterAtPosition(
            string clusterId,
            Vector3 position,
            float volumeScale = 1f)
        {
            if (TryGetClusterClip(clusterId, out AudioClip clip, out AudioPlaybackPolicy policy))
            {
                PlaySfxInternal(clip, clusterId, position, volumeScale, policy, null);
            }
        }

        public void PlaySequentialSfxInClusterAtPosition(
            string clusterId,
            Vector3 position,
            float volumeScale = 1f)
        {
            if (TryGetSequentialClip(clusterId, out AudioClip clip))
            {
                PlaySfxInternal(
                    clip,
                    clusterId,
                    position,
                    volumeScale,
                    GetClusterPolicy(clusterId),
                    null);
            }
        }

        public void PauseSfx()
        {
            sfxPaused = true;
            sfxSource?.Pause();
            for (int i = 0; i < sfxChannels.Count; i++)
            {
                sfxChannels[i]?.Pause();
            }
        }

        public void ResumeSfx()
        {
            sfxPaused = false;
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
                ReleaseSfxVoice(i);
            }

            sfxPaused = false;
        }

        public void StopAllAudio()
        {
            StopMusic();
            StopAllSfx();
            ClearPausedMusic();
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

            StopMusic();
            StopAllSfx();
            ClearPausedMusic();
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
            sfxVoiceStates.Add(new SfxVoiceState());
            return source;
        }

        private bool PlaySfxInternal(
            AudioClip clip,
            string key,
            Vector3? worldPosition,
            float volumeScale,
            AudioPlaybackPolicy policy,
            IAssetLease<AudioClip> lease)
        {
            if (clip == null || sfxMuted)
            {
                return false;
            }

            EnsureInitialized();
            ReleaseCompletedSfxVoices();
            string voiceKey = string.IsNullOrWhiteSpace(key)
                ? $"clip:{clip.GetInstanceID()}"
                : key;

            if (!CanPlaySfxKey(voiceKey, policy))
            {
                droppedSfxTotal++;
                return false;
            }

            AudioSource channel = GetAvailableChannel(policy.Priority, out int channelIndex);
            if (channel == null)
            {
                droppedSfxTotal++;
                return false;
            }

            SfxVoiceState state = sfxVoiceStates[channelIndex];
            if (state.Active)
            {
                channel.Stop();
                ReleaseSfxVoice(channelIndex);
                stolenSfxTotal++;
            }

            float safeScale = Mathf.Max(0f, volumeScale);
            channel.transform.localPosition = Vector3.zero;
            channel.spatialBlend = worldPosition.HasValue ? 1f : policy.SpatialBlend;
            if (worldPosition.HasValue)
            {
                channel.transform.position = worldPosition.Value;
            }

            channel.clip = clip;
            channel.volume = Mathf.Clamp01(sfxVolume * safeScale);
            state.Active = true;
            state.Key = voiceKey;
            state.Priority = policy.Priority;
            state.Sequence = nextVoiceSequence++;
            state.VolumeScale = safeScale;
            state.Lease = lease;
            channel.Play();
            if (lease != null)
            {
                StartCoroutine(ReleaseAssetBackedSfxWhenComplete(channelIndex, state.Sequence));
            }
            lastSfxPlayTimes[voiceKey] = Time.unscaledTimeAsDouble;
            playedSfxTotal++;
            return true;
        }

        private AudioSource GetAvailableChannel(int requestPriority, out int index)
        {
            for (int i = 0; i < sfxChannels.Count; i++)
            {
                if (!sfxVoiceStates[i].Active && sfxChannels[i] != null)
                {
                    index = i;
                    return sfxChannels[i];
                }
            }

            if (sfxChannels.Count < maxSfxChannelCount)
            {
                index = sfxChannels.Count;
                return CreateSfxChannel($"SfxChannel_Dynamic_{index}");
            }

            voiceCandidates.Clear();
            for (int i = 0; i < sfxVoiceStates.Count; i++)
            {
                SfxVoiceState state = sfxVoiceStates[i];
                voiceCandidates.Add(new AudioVoiceCandidate(
                    state.Active,
                    state.Priority,
                    state.Sequence));
            }

            index = AudioVoiceSelector.SelectStealCandidate(
                voiceCandidates,
                requestPriority);
            return index >= 0 ? sfxChannels[index] : null;
        }

        private bool CanPlaySfxKey(string key, AudioPlaybackPolicy policy)
        {
            if (policy.MinRetriggerInterval > 0f
                && lastSfxPlayTimes.TryGetValue(key, out double lastPlayTime)
                && Time.unscaledTimeAsDouble - lastPlayTime < policy.MinRetriggerInterval)
            {
                return false;
            }

            if (policy.MaxSimultaneous <= 0)
            {
                return true;
            }

            int activeForKey = 0;
            for (int i = 0; i < sfxVoiceStates.Count; i++)
            {
                SfxVoiceState state = sfxVoiceStates[i];
                if (state.Active && string.Equals(state.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    activeForKey++;
                }
            }

            return activeForKey < policy.MaxSimultaneous;
        }

        private void ReleaseCompletedSfxVoices()
        {
            if (sfxPaused)
            {
                return;
            }

            for (int i = 0; i < sfxChannels.Count; i++)
            {
                AudioSource channel = sfxChannels[i];
                if (sfxVoiceStates[i].Active && (channel == null || !channel.isPlaying))
                {
                    ReleaseSfxVoice(i);
                }
            }
        }

        private IEnumerator ReleaseAssetBackedSfxWhenComplete(int index, long sequence)
        {
            while (index >= 0 && index < sfxVoiceStates.Count)
            {
                SfxVoiceState state = sfxVoiceStates[index];
                if (!state.Active || state.Sequence != sequence)
                {
                    yield break;
                }

                AudioSource channel = index < sfxChannels.Count ? sfxChannels[index] : null;
                if (!sfxPaused && (channel == null || !channel.isPlaying))
                {
                    ReleaseSfxVoice(index);
                    yield break;
                }

                yield return null;
            }
        }

        private void ReleaseSfxVoice(int index)
        {
            if (index < 0 || index >= sfxVoiceStates.Count)
            {
                return;
            }

            SfxVoiceState state = sfxVoiceStates[index];
            state.Lease?.Dispose();
            state.Lease = null;
            state.Active = false;
            state.Key = null;
            state.Priority = 0;
            state.Sequence = 0;
            state.VolumeScale = 1f;
        }

        private async UniTask PlaySfxFromKeyAsync(
            string key,
            float volumeScale,
            Vector3? worldPosition,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetDirectEntry(key, out AudioLibrarySO.AudioEntry entry))
            {
                if (TryGetClip(key, out AudioClip clusterClip))
                {
                    PlaySfxInternal(
                        clusterClip,
                        key,
                        worldPosition,
                        volumeScale,
                        AudioPlaybackPolicy.Default,
                        null);
                }

                return;
            }

            if (entry.assetMode == AudioAssetMode.DirectClip)
            {
                PlaySfxInternal(
                    entry.clip,
                    key,
                    worldPosition,
                    volumeScale,
                    entry.Policy,
                    null);
                return;
            }

            IAssetLease<AudioClip> lease = await AcquireAudioClipAsync(
                entry.assetKey,
                cancellationToken);
            if (lease == null)
            {
                return;
            }

            if (!PlaySfxInternal(
                    lease.Asset,
                    key,
                    worldPosition,
                    volumeScale,
                    entry.Policy,
                    lease))
            {
                lease.Dispose();
            }
        }

        private async UniTask PlayMusicFromKeyAsync(
            string key,
            bool loop,
            float fadeDuration,
            float volumeScale)
        {
            try
            {
                await PlayMusicAsync(key, loop, fadeDuration, volumeScale);
            }
            catch (OperationCanceledException)
            {
                // The caller intentionally abandoned the load.
            }
            catch (Exception exception)
            {
                BaseLog.LogException(exception);
            }
        }

        private async UniTask<IAssetLease<AudioClip>> AcquireAudioClipAsync(
            string assetKey,
            CancellationToken cancellationToken)
        {
            if (assetLeaseProvider == null)
            {
                BaseLog.LogWarning(
                    $"Audio asset '{assetKey}' requires IAssetLeaseProvider, but none is configured.");
                return null;
            }

            IAssetLease<AudioClip> lease = await assetLeaseProvider.AcquireAsync<AudioClip>(
                assetKey,
                cancellationToken);
            if (lease != null && lease.IsValid && lease.Asset != null)
            {
                return lease;
            }

            lease?.Dispose();
            BaseLog.LogWarning($"Audio asset '{assetKey}' could not be loaded.");
            return null;
        }

        private void PlayMusicInternal(
            AudioClip clip,
            bool loop,
            float fadeDuration,
            float volumeScale,
            float startTime,
            IAssetLease<AudioClip> lease)
        {
            if (clip == null)
            {
                lease?.Dispose();
                return;
            }

            EnsureInitialized();
            StopMusicFade();
            musicVolumeScale = Mathf.Max(0f, volumeScale);
            float targetVolume = GetTargetMusicVolume();
            float safeStartTime = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - 0.01f));
            IAssetLease<AudioClip> previousLease = musicLease;
            musicLease = lease;

            if (fadeDuration > 0f && musicSource.isPlaying && musicSource.clip != clip)
            {
                pendingMusicLeaseRelease = previousLease;
                musicFadeRoutine = StartCoroutine(CrossFadeMusic(
                    clip,
                    loop,
                    safeStartTime,
                    targetVolume,
                    fadeDuration));
                return;
            }

            previousLease?.Dispose();
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.time = safeStartTime;
            musicSource.volume = targetVolume;
            musicSource.Play();
        }

        private bool TryGetDirectEntry(string key, out AudioLibrarySO.AudioEntry entry)
        {
            entry = default;
            return audioLibrary != null && audioLibrary.TryGetEntry(key, out entry);
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

        private bool TryGetClusterClip(
            string clusterId,
            out AudioClip clip,
            out AudioPlaybackPolicy policy)
        {
            // Cluster playback uses the authored cluster policy.
            clip = null;
            policy = AudioPlaybackPolicy.Default;
            if (audioLibrary == null
                || !audioLibrary.TryGetClusterEntry(clusterId, out AudioLibrarySO.AudioClusterEntry cluster)
                || cluster.clips == null
                || cluster.clips.Count == 0)
            {
                BaseLog.LogWarning($"Audio cluster '{clusterId}' was not found in AudioLibrary.");
                return false;
            }

            clip = cluster.clips[UnityEngine.Random.Range(0, cluster.clips.Count)];
            policy = cluster.Policy;
            return clip != null;
        }

        private AudioPlaybackPolicy GetClusterPolicy(string clusterId)
        {
            return audioLibrary != null
                && audioLibrary.TryGetClusterEntry(clusterId, out AudioLibrarySO.AudioClusterEntry cluster)
                ? cluster.Policy
                : AudioPlaybackPolicy.Default;
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
                    : Mathf.Clamp01(sfxVolume * sfxVoiceStates[i].VolumeScale);
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
            ReleasePendingMusicLease();
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
            ReleaseMusicLease();
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
            ReleasePendingMusicLease();
        }

        private void ReleaseMusicLease()
        {
            musicLease?.Dispose();
            musicLease = null;
            ReleasePendingMusicLease();
        }

        private void ReleasePendingMusicLease()
        {
            pendingMusicLeaseRelease?.Dispose();
            pendingMusicLeaseRelease = null;
        }

        private void ClearPausedMusic(bool releaseLease = true)
        {
            if (releaseLease)
            {
                pausedMusicLease?.Dispose();
            }

            pausedMusicLease = null;
            hasPausedMusic = false;
            pausedMusicClip = null;
            pausedMusicTime = 0f;
            pausedMusicVolumeScale = 1f;
        }
    }


}
