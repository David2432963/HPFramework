using UnityEngine;

namespace HP.Framework.Performance
{
    public sealed class DefaultDevicePerformanceClassifier : IDevicePerformanceClassifier
    {
        private readonly DevicePerformancePolicySO policy;

        public DefaultDevicePerformanceClassifier(DevicePerformancePolicySO policy)
        {
            this.policy = policy != null
                ? policy
                : throw new System.ArgumentNullException(nameof(policy));
        }

        public PerformanceTier RecommendTier()
        {
            int score = 0;
            score += ScoreSignal(SystemInfo.systemMemorySize, policy.LowSystemMemoryMb, policy.HighSystemMemoryMb);
            score += ScoreSignal(SystemInfo.graphicsMemorySize, policy.LowGraphicsMemoryMb, policy.HighGraphicsMemoryMb);
            score += ScoreSignal(SystemInfo.processorCount, policy.LowProcessorCount, policy.HighProcessorCount);

            if (score <= -policy.DecisiveSignalCount)
            {
                return PerformanceTier.Low;
            }

            if (score >= policy.DecisiveSignalCount)
            {
                return PerformanceTier.High;
            }

            return policy.FallbackTier;
        }

        private static int ScoreSignal(int value, int lowThreshold, int highThreshold)
        {
            if (value <= 0)
            {
                return 0;
            }

            if (lowThreshold > 0 && value <= lowThreshold)
            {
                return -1;
            }

            if (highThreshold > 0 && value >= highThreshold)
            {
                return 1;
            }

            return 0;
        }
    }
}
