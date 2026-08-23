namespace HP.Framework.Performance
{
    public interface IPerformanceRendererApplier
    {
        int Order { get; }
        void Apply(PerformanceProfileSO profile);
    }

    public interface IPerformanceTierApplier
    {
        int Order { get; }
        void Apply(PerformanceProfileSO profile);
    }
}
