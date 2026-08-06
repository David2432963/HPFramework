namespace Base.Common
{
    /// <summary>
    /// Contract for device haptic feedback and vibration.
    /// </summary>
    public interface IHapticService
    {
        bool IsHapticEnabled { get; set; }
        void VibrateShort();
        void VibrateLong();
        void VibrateCustom(long milliseconds);
    }
}
