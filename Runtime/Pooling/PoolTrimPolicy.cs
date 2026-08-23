using System;
using UnityEngine;

namespace HP.Framework.Pooling
{
    public readonly struct PoolTrimPolicy
    {
        public PoolTrimPolicy(float retainInactiveRatio, int minimumInactivePerPool = 0)
        {
            if (retainInactiveRatio < 0f || retainInactiveRatio > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(retainInactiveRatio));
            }

            if (minimumInactivePerPool < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumInactivePerPool));
            }

            RetainInactiveRatio = retainInactiveRatio;
            MinimumInactivePerPool = minimumInactivePerPool;
        }

        public float RetainInactiveRatio { get; }
        public int MinimumInactivePerPool { get; }

        public static PoolTrimPolicy Aggressive => new PoolTrimPolicy(0f);
        public static PoolTrimPolicy Balanced => new PoolTrimPolicy(0.5f);

        internal int GetTargetInactiveCount(int currentInactiveCount)
        {
            int ratioTarget = Mathf.CeilToInt(currentInactiveCount * RetainInactiveRatio);
            return Mathf.Min(
                currentInactiveCount,
                Mathf.Max(MinimumInactivePerPool, ratioTarget));
        }
    }
}
