namespace HP.Framework.Assets
{
    /// <summary>
    /// Optional memory-management surface for providers that retain zero-reference records.
    /// TrimUnused must never invalidate an active ownership reference.
    /// </summary>
    public interface IAssetMemoryService
    {
        int LoadedAssetCount { get; }
        int UnusedAssetCount { get; }
        void TrimUnused();
    }
}
