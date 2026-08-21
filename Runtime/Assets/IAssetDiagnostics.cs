namespace HP.Framework.Assets
{
    /// <summary>
    /// Read-only runtime ownership metrics intended for diagnostics and profiling surfaces.
    /// </summary>
    public interface IAssetDiagnostics
    {
        AssetProviderStats GetStats();
    }
}
