using System;

namespace HP.Framework.Haptics
{
    public interface IHapticBackend : IDisposable
    {
        bool IsAvailable { get; }
        void Play(HapticType type);
        void Vibrate(long milliseconds);
    }
}
