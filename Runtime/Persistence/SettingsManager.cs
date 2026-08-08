namespace HP.Framework.Persistence
{
    using System;
    using HP.Framework;
    using UnityEngine;
    using VContainer.Unity;
    using HP.Framework.Persistence;

    /// <summary>
    /// Single source of truth for mutable application preferences.
    /// Values are cached in memory and flushed on explicit Save or container disposal.
    /// </summary>
    public sealed class SettingsManager : ISettingsService, IInitializable, IDisposable
    {
        private readonly ISettingsStore store;

        public SettingsManager(ISettingsStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public event Action<string> SettingChanged;

        private bool soundEnabled;
        private bool musicEnabled;
        private float soundVolume;
        private float musicVolume;
        private bool vibrationEnabled;
        private float sensitivity;
        private int targetFrameRate;
        private int qualityLevel;
        private bool initialized;

        public bool IsInitialized => initialized;

        public bool SoundEnabled
        {
            get => soundEnabled;
            set => SetValue(ref soundEnabled, value, nameof(SoundEnabled),
                v => store.SetBool(nameof(SoundEnabled), v, BaseConstants.SettingsSection));
        }

        public bool MusicEnabled
        {
            get => musicEnabled;
            set => SetValue(ref musicEnabled, value, nameof(MusicEnabled),
                v => store.SetBool(nameof(MusicEnabled), v, BaseConstants.SettingsSection));
        }

        public float SoundVolume
        {
            get => soundVolume;
            set
            {
                float clamped = Mathf.Clamp01(value);
                SetValue(ref soundVolume, clamped, nameof(SoundVolume),
                    v => store.SetFloat(nameof(SoundVolume), v, BaseConstants.SettingsSection));
            }
        }

        public float MusicVolume
        {
            get => musicVolume;
            set
            {
                float clamped = Mathf.Clamp01(value);
                SetValue(ref musicVolume, clamped, nameof(MusicVolume),
                    v => store.SetFloat(nameof(MusicVolume), v, BaseConstants.SettingsSection));
            }
        }

        public bool VibrationEnabled
        {
            get => vibrationEnabled;
            set => SetValue(ref vibrationEnabled, value, nameof(VibrationEnabled),
                v => store.SetBool(nameof(VibrationEnabled), v, BaseConstants.SettingsSection));
        }

        public float Sensitivity
        {
            get => sensitivity;
            set
            {
                float clamped = Mathf.Max(0f, value);
                SetValue(ref sensitivity, clamped, nameof(Sensitivity),
                    v => store.SetFloat(nameof(Sensitivity), v, BaseConstants.SettingsSection));
            }
        }

        public int TargetFrameRate
        {
            get => targetFrameRate;
            set
            {
                int normalized = value == 0 ? -1 : value;
                if (!SetValue(ref targetFrameRate, normalized, nameof(TargetFrameRate),
                        v => store.SetInt(nameof(TargetFrameRate), v, BaseConstants.SettingsSection)))
                {
                    return;
                }

                Application.targetFrameRate = normalized;
            }
        }

        public int QualityLevel
        {
            get => qualityLevel;
            set
            {
                int maxLevel = Mathf.Max(0, QualitySettings.names.Length - 1);
                int clamped = Mathf.Clamp(value, 0, maxLevel);
                if (!SetValue(ref qualityLevel, clamped, nameof(QualityLevel),
                        v => store.SetInt(nameof(QualityLevel), v, BaseConstants.SettingsSection)))
                {
                    return;
                }

                QualitySettings.SetQualityLevel(clamped, true);
            }
        }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            soundEnabled = store.GetBool(
                nameof(SoundEnabled), true, BaseConstants.SettingsSection);
            musicEnabled = store.GetBool(
                nameof(MusicEnabled), true, BaseConstants.SettingsSection);
            soundVolume = Mathf.Clamp01(store.GetFloat(
                nameof(SoundVolume), 1f, BaseConstants.SettingsSection));
            musicVolume = Mathf.Clamp01(store.GetFloat(
                nameof(MusicVolume), 1f, BaseConstants.SettingsSection));
            vibrationEnabled = store.GetBool(
                nameof(VibrationEnabled), true, BaseConstants.SettingsSection);
            sensitivity = Mathf.Max(0f, store.GetFloat(
                nameof(Sensitivity), 1f, BaseConstants.SettingsSection));

            targetFrameRate = store.GetInt(
                nameof(TargetFrameRate), 60, BaseConstants.SettingsSection);
            int maxQualityLevel = Mathf.Max(0, QualitySettings.names.Length - 1);
            qualityLevel = Mathf.Clamp(
                store.GetInt(
                    nameof(QualityLevel),
                    QualitySettings.GetQualityLevel(),
                    BaseConstants.SettingsSection),
                0,
                maxQualityLevel);

            Application.targetFrameRate = targetFrameRate;
            QualitySettings.SetQualityLevel(qualityLevel, true);
            initialized = true;
        }

        public void Save()
        {
            store.Save();
        }

        public void Dispose()
        {
            Save();
            SettingChanged = null;
        }

        private bool SetValue<T>(
            ref T field,
            T value,
            string settingName,
            Action<T> persist)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            persist?.Invoke(value);
            SettingChanged?.Invoke(settingName);
            return true;
        }
    }


}


