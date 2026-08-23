using UnityEngine;

namespace HP.Framework.Pooling
{
    public readonly struct PoolServiceStats
    {
        public PoolServiceStats(
            int poolCount,
            int activeInstanceCount,
            int inactiveInstanceCount,
            int createdTotal,
            int trimmedTotal)
        {
            PoolCount = poolCount;
            ActiveInstanceCount = activeInstanceCount;
            InactiveInstanceCount = inactiveInstanceCount;
            CreatedTotal = createdTotal;
            TrimmedTotal = trimmedTotal;
        }

        public int PoolCount { get; }
        public int ActiveInstanceCount { get; }
        public int InactiveInstanceCount { get; }
        public int CreatedTotal { get; }
        public int TrimmedTotal { get; }
    }

    public readonly struct PoolStats
    {
        public PoolStats(
            GameObject prefab,
            int maxInactiveCount,
            int activeInstanceCount,
            int inactiveInstanceCount,
            int createdTotal,
            int trimmedTotal)
        {
            Prefab = prefab;
            MaxInactiveCount = maxInactiveCount;
            ActiveInstanceCount = activeInstanceCount;
            InactiveInstanceCount = inactiveInstanceCount;
            CreatedTotal = createdTotal;
            TrimmedTotal = trimmedTotal;
        }

        public GameObject Prefab { get; }
        public int MaxInactiveCount { get; }
        public int ActiveInstanceCount { get; }
        public int InactiveInstanceCount { get; }
        public int CreatedTotal { get; }
        public int TrimmedTotal { get; }
    }

    public interface IPoolDiagnostics
    {
        PoolServiceStats Stats { get; }
        bool TryGetStats(GameObject prefab, out PoolStats stats);
    }
}
