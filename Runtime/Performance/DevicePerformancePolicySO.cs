using UnityEngine;

namespace HP.Framework.Performance
{
    [CreateAssetMenu(menuName = "HP Framework/Performance/Device Policy", fileName = "DevicePerformancePolicy")]
    public sealed class DevicePerformancePolicySO : ScriptableObject
    {
        [SerializeField] private PerformanceTier fallbackTier = PerformanceTier.Medium;
        [SerializeField, Min(0)] private int lowSystemMemoryMb = 3000;
        [SerializeField, Min(0)] private int highSystemMemoryMb = 6000;
        [SerializeField, Min(0)] private int lowGraphicsMemoryMb = 1024;
        [SerializeField, Min(0)] private int highGraphicsMemoryMb = 4096;
        [SerializeField, Min(1)] private int lowProcessorCount = 4;
        [SerializeField, Min(1)] private int highProcessorCount = 8;
        [SerializeField, Range(1, 3)] private int decisiveSignalCount = 2;

        public PerformanceTier FallbackTier => fallbackTier;
        public int LowSystemMemoryMb => lowSystemMemoryMb;
        public int HighSystemMemoryMb => highSystemMemoryMb;
        public int LowGraphicsMemoryMb => lowGraphicsMemoryMb;
        public int HighGraphicsMemoryMb => highGraphicsMemoryMb;
        public int LowProcessorCount => lowProcessorCount;
        public int HighProcessorCount => highProcessorCount;
        public int DecisiveSignalCount => decisiveSignalCount;
    }
}
