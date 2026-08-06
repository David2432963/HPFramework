namespace Base.Persistence
{
    /// <summary>
    /// Read-only settings access for services that need settings without circular dependencies.
    /// </summary>
    public interface ISettingsProvider
    {
        bool SoundEnabled { get; }
        bool MusicEnabled { get; }
        float SoundVolume { get; }
        float MusicVolume { get; }
        bool VibrationEnabled { get; }
        float Sensitivity { get; }
        int TargetFrameRate { get; }
        int QualityLevel { get; }
    }
}
