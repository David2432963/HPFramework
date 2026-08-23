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
    public sealed class SettingsManager : ISettingsService, ISettingsFlushService, IInitializable, IDisposable
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
        private bool useAutomaticPerformanceTier;
        private int preferredPerformanceTier;
        private bool initialized;
        private bool dirty;

        public bool IsInitialized => initialized;
        public bool IsDirty => dirty;

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

        /// <summary>
        /// Compatibility preference only. Runtime frame-rate application is owned by PerformanceService.
        /// </summary>
        public int TargetFrameRate
        {
            get => targetFrameRate;
            set
            {
                int normalized = value == 0 ? -1 : value;
                SetValue(ref targetFrameRate, normalized, nameof(TargetFrameRate),
                    v => store.SetInt(nameof(TargetFrameRate), v, BaseConstants.SettingsSection));
            }
        }

        /// <summary>
        /// Compatibility preference only. Runtime quality application is owned by PerformanceService.
        /// </summary>
        public int QualityLevel
        {
            get => qualityLevel;
            set
            {
                SetValue(ref qualityLevel, value, nameof(QualityLevel),
                    v => store.SetInt(nameof(QualityLevel), v, BaseConstants.SettingsSection));
            }
        }

        public bool UseAutomaticPerformanceTier
        {
            get => useAutomaticPerformanceTier;
            set => SetValue(ref useAutomaticPerformanceTier, value, nameof(UseAutomaticPerformanceTier),
                v => store.SetBool(nameof(UseAutomaticPerformanceTier), v, BaseConstants.SettingsSection));
        }

        public int PreferredPerformanceTier
        {
            get => preferredPerformanceTier;
            set => SetValue(ref preferredPerformanceTier, value, nameof(PreferredPerformanceTier),
                v => store.SetInt(nameof(PreferredPerformanceTier), v, BaseConstants.SettingsSection));
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
            qualityLevel = store.GetInt(
                nameof(QualityLevel), 0, BaseConstants.SettingsSection);
            useAutomaticPerformanceTier = store.GetBool(
                nameof(UseAutomaticPerformanceTier), true, BaseConstants.SettingsSection);
            preferredPerformanceTier = store.GetInt(
                nameof(PreferredPerformanceTier), 1, BaseConstants.SettingsSection);

            initialized = true;
            dirty = false;
        }

        public void Save()
        {
            PersistCachedValues();
            store.Save();
            dirty = false;
        }

        public bool SaveIfDirty()
        {
            if (!dirty)
            {
                return false;
            }

            Save();
            return true;
        }

        public void Dispose()
        {
            try
            {
                SaveIfDirty();
            }
            finally
            {
                SettingChanged = null;
            }
        }

        private void PersistCachedValues()
        {
            store.SetBool(nameof(SoundEnabled), soundEnabled, BaseConstants.SettingsSection);
            store.SetBool(nameof(MusicEnabled), musicEnabled, BaseConstants.SettingsSection);
            store.SetFloat(nameof(SoundVolume), soundVolume, BaseConstants.SettingsSection);
            store.SetFloat(nameof(MusicVolume), musicVolume, BaseConstants.SettingsSection);
            store.SetBool(nameof(VibrationEnabled), vibrationEnabled, BaseConstants.SettingsSection);
            store.SetFloat(nameof(Sensitivity), sensitivity, BaseConstants.SettingsSection);
            store.SetInt(nameof(TargetFrameRate), targetFrameRate, BaseConstants.SettingsSection);
            store.SetInt(nameof(QualityLevel), qualityLevel, BaseConstants.SettingsSection);
            store.SetBool(nameof(UseAutomaticPerformanceTier), useAutomaticPerformanceTier, BaseConstants.SettingsSection);
            store.SetInt(nameof(PreferredPerformanceTier), preferredPerformanceTier, BaseConstants.SettingsSection);
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
            dirty = true;
            persist?.Invoke(value);
            SettingChanged?.Invoke(settingName);
            return true;
        }
    }


}


