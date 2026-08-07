using System;

namespace Base.Persistence
{
    /// <summary>
    /// Mutable application settings contract. Services may subscribe to changes instead of
    /// reading PlayerPrefs independently.
    /// </summary>
    public interface ISettingsService : ISettingsProvider
    {
        event Action<string> SettingChanged;

        new bool SoundEnabled { get; set; }
        new bool MusicEnabled { get; set; }
        new float SoundVolume { get; set; }
        new float MusicVolume { get; set; }
        new bool VibrationEnabled { get; set; }
        new float Sensitivity { get; set; }
        new int TargetFrameRate { get; set; }
        new int QualityLevel { get; set; }

        void Save();
    }
}
