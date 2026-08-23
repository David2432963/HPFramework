using HP.Framework.Assets;
using HP.Framework.Audio;
using HP.Framework.Input;
using HP.Framework.Pooling;

namespace HP.Framework.Samples.MobileDiagnostics
{
    public sealed class SampleDiagnosticsReader
    {
        public SampleDiagnosticsReader(
            IAssetDiagnostics assets,
            IPoolDiagnostics pools,
            IAudioDiagnostics audio,
            IInputDiagnostics input)
        {
            AssetStats = assets.GetStats();
            PoolStats = pools.Stats;
            AudioStats = audio.Stats;
            InputStats = input.Stats;
        }

        public AssetProviderStats AssetStats { get; }
        public PoolServiceStats PoolStats { get; }
        public AudioServiceStats AudioStats { get; }
        public InputServiceStats InputStats { get; }
    }
}
