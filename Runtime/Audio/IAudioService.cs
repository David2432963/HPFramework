using UnityEngine;

namespace HP.Framework.Audio
{
    /// <summary>
    /// Contract for global audio management operations (Music & SFX).
    /// </summary>
    public interface IAudioService
    {
        bool IsMusicPlaying { get; }
        bool IsMusicMuted { get; }
        bool IsSfxMuted { get; }

        void PlayMusic(AudioClip clip, bool loop = true, float fadeDuration = 0.5f, float volumeScale = 1f, float startTime = 0f);
        void PlayMusic(string key, bool loop = true, float fadeDuration = 0.5f, float volumeScale = 1f);
        void OverrideMusic(AudioClip clip, bool loop = true, float fadeDuration = 0.5f, float volumeScale = 1f);
        void RestoreMusic(float fadeDuration = 0.5f);
        void PauseMusic();
        void ResumeMusic();
        void StopMusic();

        void PlaySfx(AudioClip clip, float volumeScale = 1f);
        void PlaySfx(string key, float volumeScale = 1f);
        void PlayRandomSfxInCluster(string clusterId, float volumeScale = 1f);
        void PlaySequentialSfxInCluster(string clusterId, float volumeScale = 1f);

        void PlaySfxAtPosition(AudioClip clip, Vector3 position, float volumeScale = 1f);
        void PlaySfxAtPosition(string key, Vector3 position, float volumeScale = 1f);
        void PlayRandomSfxInClusterAtPosition(string clusterId, Vector3 position, float volumeScale = 1f);
        void PlaySequentialSfxInClusterAtPosition(string clusterId, Vector3 position, float volumeScale = 1f);

        void PauseSfx();
        void ResumeSfx();
        void StopAllSfx();
        void StopAllAudio();

        void SetMusicMuted(bool muted);
        void SetSfxMuted(bool muted);
        void SetMusicVolume(float volume);
        void SetSfxVolume(float volume);

        float GetMusicVolume();
        float GetSfxVolume();
    }
}


