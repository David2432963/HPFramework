using UnityEngine;
using VContainer;
using Base.Common;
using Base.Persistence;
using Base;

/// <summary>
/// Global manager for controlling device vibration and haptics.
/// Implements IHapticService managed by VContainer.
/// </summary>
public sealed class HapticManager : MonoBehaviour, IHapticService
{
    private bool isHapticEnabled = true;
    private ISettingsProvider settingsProvider;

    [Inject]
    public void Construct(ISettingsProvider settingsProvider)
    {
        this.settingsProvider = settingsProvider;
        if (settingsProvider != null)
        {
            isHapticEnabled = settingsProvider.VibrationEnabled;
        }
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
            PlayerPrefs.SetInt(BaseConstants.HapticPrefsKey, isHapticEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public void Initialize()
    {
        if (settingsProvider != null)
        {
            isHapticEnabled = settingsProvider.VibrationEnabled;
        }
        else
        {
            isHapticEnabled = PlayerPrefs.GetInt(BaseConstants.HapticPrefsKey, 1) == 1;
        }
    }

    public void VibrateShort()
    {
        if (!isHapticEnabled)
        {
            return;
        }

        VibrationHelper.Vibrate(30);
    }

    public void VibrateLong()
    {
        if (!isHapticEnabled)
        {
            return;
        }

        VibrationHelper.Vibrate(150);
    }

    public void VibrateCustom(long milliseconds)
    {
        if (!isHapticEnabled)
        {
            return;
        }

        VibrationHelper.Vibrate(milliseconds);
    }
}
