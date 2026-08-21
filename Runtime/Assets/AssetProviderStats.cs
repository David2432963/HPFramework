namespace HP.Framework.Assets
{
    /// <summary>
    /// Allocation-free snapshot of provider ownership state for diagnostics tooling.
    /// </summary>
    public readonly struct AssetProviderStats
    {
        public AssetProviderStats(
            int loadedAssetCount,
            int unusedAssetCount,
            int activeReferenceCount,
            int pendingLoadCount,
            int trackedInstanceCount)
        {
            LoadedAssetCount = loadedAssetCount;
            UnusedAssetCount = unusedAssetCount;
            ActiveReferenceCount = activeReferenceCount;
            PendingLoadCount = pendingLoadCount;
            TrackedInstanceCount = trackedInstanceCount;
        }

        public int LoadedAssetCount { get; }
        public int UnusedAssetCount { get; }
        public int ActiveReferenceCount { get; }
        public int PendingLoadCount { get; }
        public int TrackedInstanceCount { get; }
    }
}
