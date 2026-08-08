namespace HP.Framework.Haptics
{
    using System;
    using UnityEngine;
    using VContainer;
    using VContainer.Unity;
    using HP.Framework.Common;
    using HP.Framework.Persistence;

    /// <summary>
    /// Haptic adapter driven by the shared settings service.
    /// </summary>
    public sealed class HapticManager : MonoBehaviour, IHapticService, IInitializable, IDisposable
    {
        private ISettingsService settingsService;
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

            VibrationHelper.Vibrate(milliseconds);
        }

        public void Dispose()
        {
            if (settingsService != null)
            {
                settingsService.SettingChanged -= OnSettingChanged;
            }
        }

        private void OnSettingChanged(string settingName)
        {
            if (settingName == nameof(ISettingsProvider.VibrationEnabled))
            {
                isHapticEnabled = settingsService.VibrationEnabled;
            }
        }
    }


}


