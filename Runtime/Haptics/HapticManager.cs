namespace HP.Framework.Haptics
{
    using System;
    using UnityEngine;
    using VContainer;
    using VContainer.Unity;
    using HP.Framework.Persistence;

    /// <summary>
    /// Haptic adapter driven by the shared settings service.
    /// </summary>
    public sealed class HapticManager : MonoBehaviour, IHapticService, IInitializable, IDisposable
    {
        private ISettingsService settingsService;
        private IHapticBackend backend;
        private bool isHapticEnabled = true;

        [Inject]
        public void Construct(ISettingsService settingsService)
        {
            this.settingsService = settingsService;
        }

        public bool IsHapticEnabled
        {
            get => isHapticEnabled;
            set
            {
                if (isHapticEnabled == value)
                {
                    return;
                }

                isHapticEnabled = value;
                if (settingsService != null)
                {
                    settingsService.VibrationEnabled = value;
                }
            }
        }

        public void Initialize()
        {
            EnsureBackend();
            if (settingsService == null)
            {
                return;
            }

            isHapticEnabled = settingsService.VibrationEnabled;
            settingsService.SettingChanged -= OnSettingChanged;
            settingsService.SettingChanged += OnSettingChanged;
        }

        public void VibrateShort()
        {
            VibrateCustom(30);
        }

        public void VibrateLong()
        {
            VibrateCustom(150);
        }

        public void VibrateCustom(long milliseconds)
        {
            if (!isHapticEnabled || milliseconds <= 0)
            {
                return;
            }

            EnsureBackend();
            if (backend.IsAvailable)
            {
                backend.Vibrate(milliseconds);
            }
        }

        public void Play(HapticType type)
        {
            if (!isHapticEnabled)
            {
                return;
            }

            EnsureBackend();
            if (backend.IsAvailable)
            {
                backend.Play(type);
            }
        }

        public void ConfigureBackend(IHapticBackend hapticBackend)
        {
            if (ReferenceEquals(backend, hapticBackend))
            {
                return;
            }

            backend?.Dispose();
            backend = hapticBackend ?? throw new ArgumentNullException(nameof(hapticBackend));
        }

        public void Dispose()
        {
            if (settingsService != null)
            {
                settingsService.SettingChanged -= OnSettingChanged;
            }

            backend?.Dispose();
            backend = null;
        }

        private void OnSettingChanged(string settingName)
        {
            if (settingName == nameof(ISettingsProvider.VibrationEnabled))
            {
                isHapticEnabled = settingsService.VibrationEnabled;
            }
        }

        private void EnsureBackend()
        {
            backend ??= HapticBackendFactory.Create();
        }
    }


}

