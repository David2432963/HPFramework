namespace HP.Framework.Haptics
{
    /// <summary>
    /// Contract for device haptic feedback and vibration.
    /// </summary>
    public interface IHapticService
    {
        bool IsHapticEnabled { get; set; }
        void Play(HapticType type);
        void VibrateShort();
        void VibrateLong();
        void VibrateCustom(long milliseconds);
    }
}

