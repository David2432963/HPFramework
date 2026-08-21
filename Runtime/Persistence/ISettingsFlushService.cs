namespace HP.Framework.Persistence
{
    /// <summary>
    /// Persistence boundary for lifecycle-safe settings flushes without exposing storage details.
    /// </summary>
    public interface ISettingsFlushService
    {
        bool IsDirty { get; }
        void Save();
        bool SaveIfDirty();
    }
}
