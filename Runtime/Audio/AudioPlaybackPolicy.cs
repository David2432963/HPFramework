using UnityEngine;

namespace HP.Framework.Audio
{
    public enum AudioAssetMode
    {
        DirectClip = 0,
        AssetKey = 1
    }

    public enum AudioCategory
    {
        Gameplay = 0,
        UI = 1,
        Ambient = 2,
        Voice = 3
    }

    public readonly struct AudioPlaybackPolicy
    {
        public AudioPlaybackPolicy(
            int priority,
            int maxSimultaneous,
            float minRetriggerInterval,
            float spatialBlend,
            AudioCategory category)
        {
            Priority = priority;
            MaxSimultaneous = Mathf.Max(0, maxSimultaneous);
            MinRetriggerInterval = Mathf.Max(0f, minRetriggerInterval);
            SpatialBlend = Mathf.Clamp01(spatialBlend);
            Category = category;
        }

        public int Priority { get; }
        public int MaxSimultaneous { get; }
        public float MinRetriggerInterval { get; }
        public float SpatialBlend { get; }
        public AudioCategory Category { get; }

        public static AudioPlaybackPolicy Default =>
            new AudioPlaybackPolicy(0, 0, 0f, 0f, AudioCategory.Gameplay);
    }
}
