namespace HP.Framework.Input
{
    public readonly struct InputServiceStats
    {
        public InputServiceStats(
            string primaryMapName,
            int leasedMapCount,
            int activeLeaseCount,
            int compatibilityMapCount)
        {
            PrimaryMapName = primaryMapName;
            LeasedMapCount = leasedMapCount;
            ActiveLeaseCount = activeLeaseCount;
            CompatibilityMapCount = compatibilityMapCount;
        }

        public string PrimaryMapName { get; }
        public int LeasedMapCount { get; }
        public int ActiveLeaseCount { get; }
        public int CompatibilityMapCount { get; }
    }

    public interface IInputDiagnostics
    {
        InputServiceStats Stats { get; }
    }

    public sealed class UnavailableInputDiagnostics : IInputDiagnostics
    {
        public UnavailableInputDiagnostics()
        {
        }

        public InputServiceStats Stats => default;
    }
}
