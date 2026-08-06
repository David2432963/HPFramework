using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using VContainer;
using Base.Audio;
using Base.Persistence;
using Base;

using Sirenix.OdinInspector;

/// <summary>
/// Global audio service for music and SFX.
/// Managed as a VContainer component implementing IAudioService.
/// Uses Odin Inspector attributes for rich editor formatting.
/// </summary>
public class AudioManager : MonoBehaviour, IAudioService
{
    [System.Serializable]
    public struct AudioSettingsData
    {
        public bool MusicMuted;
        public float MusicVolume;
        public bool SfxMuted;
        public float SfxVolume;
    }

    [Title("🎵 Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Title("🔊 Volume Settings")]
    [SerializeField] private bool musicMuted;
    [SerializeField, Range(0f, 1f), ProgressBar(0, 1)] private float musicVolume = 1f;
    [SerializeField] private bool sfxMuted;
    [SerializeField, Range(0f, 1f), ProgressBar(0, 1)] private float sfxVolume = 1f;

    [Title("SFX Channel Pool")]
    [SerializeField, MinValue(1)] private int sfxChannelCount = 10;
    [SerializeField, MinValue(1)] private int maxSfxChannelCount = 30;

    [Title("Audio Database")]
    [SerializeField, InlineEditor] private AudioLibrarySO audioLibrary;

    private readonly List<AudioSource> sfxChannels = new List<AudioSource>();
    private readonly List<float> sfxChannelVolumeScales = new List<float>();
    private float musicVolumeScale = 1f;
    private Tween musicFadeTween;

    private AudioClip pausedMusicClip;
    private float pausedMusicTime;
    private float pausedMusicVolumeScale;
    private bool hasPausedMusic;

    private readonly Dictionary<string, int> clusterSequentialIndices = new Dictionary<string, int>();
    private ISettingsProvider settingsProvider;

    public bool IsMusicPlaying => musicSource != null && musicSource.isPlaying;
    public bool IsMusicMuted => musicMuted;
    public bool IsSfxMuted => sfxMuted;

    [Inject]
    public void Construct(ISettingsProvider settingsProvider, AudioLibrarySO library = null)
    {
        this.settingsProvider = settingsProvider;
        if (library != null)
        {
            this.audioLibrary = library;
            this.audioLibrary.InitializeLookup();
        }

        SyncWithSettings();
    }

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        sfxChannelCount = Mathf.Clamp(sfxChannelCount, 1, Mathf.Max(1, maxSfxChannelCount));
        maxSfxChannelCount = Mathf.Max(1, maxSfxChannelCount);
        EnsureAudioSources();

        SyncWithSettings();
        ApplySettings();
        InitializeSfxPool();
    }

    private void SyncWithSettings()
    {
        if (settingsProvider != null)
        {
            musicMuted = !settingsProvider.MusicEnabled;
            musicVolume = settingsProvider.MusicVolume;
            sfxMuted = !settingsProvider.SoundEnabled;
            sfxVolume = settingsProvider.SoundVolume;
        }
    }

    public void ConfigureLibrary(AudioLibrarySO library)
    {
        if (library == null)
        {
            throw new System.ArgumentNullException(nameof(library));
        }

        audioLibrary = library;
        audioLibrary.InitializeLookup();
    }

    private void InitializeSfxPool()
    {
        if (sfxChannels.Count > 0)
        {
            return;
        }

        sfxChannelVolumeScales.Clear();
        for (int i = 0; i < sfxChannelCount; i++)
        {
            var channelGo = new GameObject($"SfxChannel_{i}");
            channelGo.transform.SetParent(transform);
            var source = channelGo.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            sfxChannels.Add(source);
            sfxChannelVolumeScales.Add(1f);
        }
    }

    private void KillMusicFadeTween()
    {
        if (musicFadeTween != null)
        {
            musicFadeTween.Kill();
            musicFadeTween = null;
        }
    }

    public void PlayMusic(AudioClip clip, bool loop = true, float fadeDuration = 0.5f, float volumeScale = 1f, float startTime = 0f)
    {
        if (clip == null)
        {
            return;
        }

        EnsureAudioSources();
        KillMusicFadeTween();

        musicVolumeScale = volumeScale;
        float targetVolume = (musicMuted ? 0f : musicVolume) * musicVolumeScale;

        if (fadeDuration > 0f && musicSource.isPlaying && musicSource.clip != clip)
        {
            musicFadeTween = musicSource.DOFade(0f, fadeDuration)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    musicSource.clip = clip;
                    musicSource.loop = loop;
                    musicSource.time = startTime;
                    musicSource.Play();
                    musicFadeTween = musicSource.DOFade(targetVolume, fadeDuration)
                        .SetEase(Ease.OutQuad);
                });
        }
        else
        {
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.time = startTime;
            musicSource.volume = targetVolume;
            if (!musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }
    }

    public void OverrideMusic(AudioClip clip, bool loop = true, float fadeDuration = 0.5f, float volumeScale = 1f)
    {
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
            PlayMusic(pausedMusicClip, true, fadeDuration, pausedMusicVolumeScale, pausedMusicTime);
        }
        else
        {
            if (fadeDuration > 0f)
            {
                KillMusicFadeTween();
                musicFadeTween = musicSource.DOFade(0f, fadeDuration).OnComplete(StopMusic);
            }
            else
            {
                StopMusic();
            }
        }
        hasPausedMusic = false;
        pausedMusicClip = null;
    }

    public void PlayMusic(string key, bool loop = true, float fadeDuration = 0.5f, float volumeScale = 1f)
    {
        if (audioLibrary != null && audioLibrary.TryGetClip(key, out AudioClip clip))
        {
            PlayMusic(clip, loop, fadeDuration, volumeScale);
        }
        else
        {
            BaseLog.LogWarning($"Music key '{key}' not found in AudioLibrary.");
        }
    }

    public void PauseMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Pause();
        }
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
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || sfxMuted)
        {
            return;
        }

        EnsureAudioSources();

        AudioSource freeChannel = GetFreeChannel(out int channelIndex);
        if (freeChannel != null)
        {
            freeChannel.spatialBlend = 0f;
            freeChannel.clip = clip;
            if (channelIndex < sfxChannelVolumeScales.Count)
            {
                sfxChannelVolumeScales[channelIndex] = volumeScale;
            }
            freeChannel.volume = sfxVolume * volumeScale;
            freeChannel.Play();
        }
        else
        {
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(sfxVolume * volumeScale));
        }
    }

    public void PlaySfx(string key, float volumeScale = 1f)
    {
        if (audioLibrary != null && audioLibrary.TryGetClip(key, out AudioClip clip))
        {
            PlaySfx(clip, volumeScale);
        }
        else
        {
            BaseLog.LogWarning($"SFX key '{key}' not found in AudioLibrary.");
        }
    }

    public void PlayRandomSfxInCluster(string clusterId, float volumeScale = 1f)
    {
        PlaySfx(clusterId, volumeScale);
    }

    public void PlaySfxAtPosition(AudioClip clip, Vector3 position, float volumeScale = 1f)
    {
        if (clip == null || sfxMuted)
        {
            return;
        }

        EnsureAudioSources();

        AudioSource freeChannel = GetFreeChannel(out int channelIndex);
        if (freeChannel != null)
        {
            freeChannel.transform.position = position;
            freeChannel.spatialBlend = 1f;
            freeChannel.clip = clip;
            if (channelIndex < sfxChannelVolumeScales.Count)
            {
                sfxChannelVolumeScales[channelIndex] = volumeScale;
            }
            freeChannel.volume = sfxVolume * volumeScale;
            freeChannel.Play();
        }
        else
        {
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(sfxVolume * volumeScale));
        }
    }

    public void PlaySfxAtPosition(string key, Vector3 position, float volumeScale = 1f)
    {
        if (audioLibrary != null && audioLibrary.TryGetClip(key, out AudioClip clip))
        {
            PlaySfxAtPosition(clip, position, volumeScale);
        }
        else
        {
            BaseLog.LogWarning($"SFX key '{key}' not found in AudioLibrary.");
        }
    }

    public void PlayRandomSfxInClusterAtPosition(string clusterId, Vector3 position, float volumeScale = 1f)
    {
        PlaySfxAtPosition(clusterId, position, volumeScale);
    }

    public void PlaySequentialSfxInClusterAtPosition(string clusterId, Vector3 position, float volumeScale = 1f)
    {
        if (audioLibrary != null)
        {
            if (!clusterSequentialIndices.TryGetValue(clusterId, out int currentIndex))
            {
                currentIndex = -1;
            }
            if (audioLibrary.TryGetSequentialClip(clusterId, ref currentIndex, out AudioClip clip))
            {
                clusterSequentialIndices[clusterId] = currentIndex;
                PlaySfxAtPosition(clip, position, volumeScale);
            }
            else
            {
                BaseLog.LogWarning($"SFX key '{clusterId}' not found in AudioLibrary for sequential play.");
            }
        }
    }

    public void PlaySequentialSfxInCluster(string clusterId, float volumeScale = 1f)
    {
        if (audioLibrary != null)
        {
            if (!clusterSequentialIndices.TryGetValue(clusterId, out int currentIndex))
            {
                currentIndex = -1;
            }
            if (audioLibrary.TryGetSequentialClip(clusterId, ref currentIndex, out AudioClip clip))
            {
                clusterSequentialIndices[clusterId] = currentIndex;
                PlaySfx(clip, volumeScale);
            }
            else
            {
                BaseLog.LogWarning($"SFX key '{clusterId}' not found in AudioLibrary for sequential play.");
            }
        }
    }

    private AudioSource GetFreeChannel(out int index)
    {
        for (int i = 0; i < sfxChannels.Count; i++)
        {
            if (sfxChannels[i] != null && !sfxChannels[i].isPlaying)
            {
                index = i;
                return sfxChannels[i];
            }
        }

        if (sfxChannels.Count < maxSfxChannelCount)
        {
            int newIndex = sfxChannels.Count;
            var channelGo = new GameObject($"SfxChannel_Dynamic_{newIndex}");
            channelGo.transform.SetParent(transform);
            var source = channelGo.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.mute = sfxMuted;
            sfxChannels.Add(source);
            sfxChannelVolumeScales.Add(1f);
            index = newIndex;
            return source;
        }

        index = -1;
        return null;
    }

    public void PauseSfx()
    {
        if (sfxSource != null && sfxSource.isPlaying)
        {
            sfxSource.Pause();
        }

        for (int i = 0; i < sfxChannels.Count; i++)
        {
            if (sfxChannels[i] != null && sfxChannels[i].isPlaying)
            {
                sfxChannels[i].Pause();
            }
        }
    }

    public void ResumeSfx()
    {
        if (sfxSource != null && sfxSource.clip != null && !sfxSource.isPlaying)
        {
            sfxSource.UnPause();
        }

        for (int i = 0; i < sfxChannels.Count; i++)
        {
            if (sfxChannels[i] != null && sfxChannels[i].clip != null && !sfxChannels[i].isPlaying)
            {
                sfxChannels[i].UnPause();
            }
        }
    }

    public void StopAllSfx()
    {
        if (sfxSource != null)
        {
            sfxSource.Stop();
        }

        for (int i = 0; i < sfxChannels.Count; i++)
        {
            if (sfxChannels[i] != null)
            {
                sfxChannels[i].Stop();
            }
        }
    }

    public void StopAllAudio()
    {
        StopMusic();
        StopAllSfx();
    }

    public void SetMusicMuted(bool muted)
    {
        musicMuted = muted;
        ApplySettings();
    }

    public void SetSfxMuted(bool muted)
    {
        sfxMuted = muted;
        ApplySettings();
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        ApplySettings();
    }

    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        ApplySettings();
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
        musicMuted = settings.MusicMuted;
        musicVolume = Mathf.Clamp01(settings.MusicVolume);
        sfxMuted = settings.SfxMuted;
        sfxVolume = Mathf.Clamp01(settings.SfxVolume);
        ApplySettings();
    }

    private void EnsureAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = GetComponent<AudioSource>();
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (sfxSource == null)
        {
            var sources = GetComponents<AudioSource>();
            if (sources.Length > 1)
            {
                sfxSource = sources[1];
            }
            else
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }
        }

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
    }

    private void ApplySettings()
    {
        EnsureAudioSources();
        musicSource.volume = (musicMuted ? 0f : musicVolume) * musicVolumeScale;
        sfxSource.volume = sfxMuted ? 0f : sfxVolume;
        musicSource.mute = musicMuted;
        sfxSource.mute = sfxMuted;

        for (int i = 0; i < sfxChannels.Count; i++)
        {
            if (sfxChannels[i] != null)
            {
                sfxChannels[i].mute = sfxMuted;
                if (i < sfxChannelVolumeScales.Count)
                {
                    sfxChannels[i].volume = sfxVolume * sfxChannelVolumeScales[i];
                }
            }
        }
    }

    private void OnDestroy()
    {
        KillMusicFadeTween();
    }
}
