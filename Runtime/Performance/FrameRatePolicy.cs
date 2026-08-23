using System;
using UnityEngine;

namespace HP.Framework.Performance
{
    public static class FrameRatePolicy
    {
        private const int MinimumPacedTarget = 30;
        private const int MaxDivisor = 4;

        public static int NormalizeTarget(int requested, double refreshRate, bool allowUnlimited)
        {
            if (requested <= 0)
            {
                if (allowUnlimited)
                {
                    return -1;
                }

                requested = 60;
            }

            if (double.IsNaN(refreshRate) || double.IsInfinity(refreshRate) || refreshRate < MinimumPacedTarget)
            {
                return Mathf.Max(1, requested);
            }

            int best = Mathf.Max(1, Mathf.RoundToInt((float)refreshRate));
            double bestDistance = Math.Abs(best - requested);

            for (int divisor = 1; divisor <= MaxDivisor; divisor++)
            {
                double candidateExact = refreshRate / divisor;
                if (candidateExact < MinimumPacedTarget)
                {
                    continue;
                }

                int candidate = Mathf.Max(1, Mathf.RoundToInt((float)candidateExact));
                double distance = Math.Abs(candidate - requested);
                if (distance < bestDistance || (Math.Abs(distance - bestDistance) < 0.001 && candidate < best))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }
    }
}
