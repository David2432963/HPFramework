using System;
using UnityEngine;
using VContainer.Unity;
using Base.Persistence;

/// <summary>
/// Settings manager service for common application preferences.
/// Pure C# class managed by VContainer as ISettingsProvider.
/// Caches settings in RAM to avoid registry read/write overhead and GC allocations.
/// </summary>
public sealed class SettingsManager : ISettingsProvider, IInitializable, IDisposable
{
    public event Action<string> SettingChanged;

    private bool soundEnabled;
    private bool musicEnabled;
    private float soundVolume;
    private float musicVolume;
    private bool vibrationEnabled;
    private float sensitivity;

    private int targetFrameRate;
    private int qualityLevel;

    public bool SoundEnabled
    {
        get => soundEnabled;
        set
        {
            if (soundEnabled != value)
            {
                soundEnabled = value;
                PlayerPrefSave.SetBool(nameof(SoundEnabled), value, BaseConstants.SettingsSection);
                SettingChanged?.Invoke(nameof(SoundEnabled));
            }
        }
    }

    public bool MusicEnabled
    {
        get => musicEnabled;
        set
        {
            if (musicEnabled != value)
            {
                musicEnabled = value;
                PlayerPrefSave.SetBool(nameof(MusicEnabled), value, BaseConstants.SettingsSection);
                SettingChanged?.Invoke(nameof(MusicEnabled));
            }
        }
    }

    public float SoundVolume
    {
        get => soundVolume;
        set
        {
            float clamped = Mathf.Clamp01(value);
            if (!Mathf.Approximately(soundVolume, clamped))
            {
                soundVolume = clamped;
                PlayerPrefSave.SetFloat(nameof(SoundVolume), clamped, BaseConstants.SettingsSection);
                SettingChanged?.Invoke(nameof(SoundVolume));
            }
        }
    }

    public float MusicVolume
    {
        get => musicVolume;
        set
        {
            float clamped = Mathf.Clamp01(value);
            if (!Mathf.Approximately(musicVolume, clamped))
            {
                musicVolume = clamped;
                PlayerPrefSave.SetFloat(nameof(MusicVolume), clamped, BaseConstants.SettingsSection);
                SettingChanged?.Invoke(nameof(MusicVolume));
            }
        }
    }

    public bool VibrationEnabled
    {
        get => vibrationEnabled;
        set
        {
            if (vibrationEnabled != value)
            {
                vibrationEnabled = value;
                PlayerPrefSave.SetBool(nameof(VibrationEnabled), value, BaseConstants.SettingsSection);
                SettingChanged?.Invoke(nameof(VibrationEnabled));
            }
        }
    }

    public float Sensitivity
    {
        get => sensitivity;
        set
        {
            if (!Mathf.Approximately(sensitivity, value))
            {
                sensitivity = value;
                PlayerPrefSave.SetFloat(nameof(Sensitivity), value, BaseConstants.SettingsSection);
                SettingChanged?.Invoke(nameof(Sensitivity));
            }
        }
    }

    public int TargetFrameRate
    {
        get => targetFrameRate;
        set
        {
            if (targetFrameRate != value)
            {
                targetFrameRate = value;
                PlayerPrefSave.SetInt(nameof(TargetFrameRate), value, BaseConstants.SettingsSection);
                Application.targetFrameRate = value;
                SettingChanged?.Invoke(nameof(TargetFrameRate));
            }
        }
    }

    public int QualityLevel
    {
        get => qualityLevel;
        set
        {
            if (qualityLevel != value)
            {
                qualityLevel = value;
                PlayerPrefSave.SetInt(nameof(QualityLevel), value, BaseConstants.SettingsSection);
                QualitySettings.SetQualityLevel(value, true);
                SettingChanged?.Invoke(nameof(QualityLevel));
            }
        }
    }

    public void Initialize()
    {
        soundEnabled = PlayerPrefSave.GetBool(nameof(SoundEnabled), true, BaseConstants.SettingsSection);
        musicEnabled = PlayerPrefSave.GetBool(nameof(MusicEnabled), true, BaseConstants.SettingsSection);
        soundVolume = PlayerPrefSave.GetFloat(nameof(SoundVolume), 1f, BaseConstants.SettingsSection);
        musicVolume = PlayerPrefSave.GetFloat(nameof(MusicVolume), 1f, BaseConstants.SettingsSection);
        vibrationEnabled = PlayerPrefSave.GetBool(nameof(VibrationEnabled), true, BaseConstants.SettingsSection);
        sensitivity = PlayerPrefSave.GetFloat(nameof(Sensitivity), 1f, BaseConstants.SettingsSection);

        targetFrameRate = PlayerPrefSave.GetInt(nameof(TargetFrameRate), 60, BaseConstants.SettingsSection);
        qualityLevel = PlayerPrefSave.GetInt(nameof(QualityLevel), QualitySettings.GetQualityLevel(), BaseConstants.SettingsSection);

        Application.targetFrameRate = targetFrameRate;
        QualitySettings.SetQualityLevel(qualityLevel, true);
    }

    public void Save()
    {
        PlayerPrefSave.Save();
    }

    public void Dispose()
    {
        Save();
    }
}
