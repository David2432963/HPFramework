using System;

namespace HP.Framework.Pooling
{
    public readonly struct PoolPrewarmOptions
    {
        public const int DefaultMaxInstancesPerFrame = 8;

        public PoolPrewarmOptions(int maxInstancesPerFrame)
        {
            if (maxInstancesPerFrame <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInstancesPerFrame));
            }

            MaxInstancesPerFrame = maxInstancesPerFrame;
        }

        public int MaxInstancesPerFrame { get; }

        public static PoolPrewarmOptions Default =>
            new PoolPrewarmOptions(DefaultMaxInstancesPerFrame);

        internal int ResolveMaxInstancesPerFrame()
        {
            return MaxInstancesPerFrame > 0
                ? MaxInstancesPerFrame
                : DefaultMaxInstancesPerFrame;
        }
    }
}
