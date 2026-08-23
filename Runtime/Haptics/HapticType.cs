namespace HP.Framework.Haptics
{
    public enum HapticType
    {
        Selection = 0,
        LightImpact = 1,
        MediumImpact = 2,
        HeavyImpact = 3,
        Success = 4,
        Warning = 5,
        Error = 6
    }

    internal static class HapticPattern
    {
        public static long GetDurationMilliseconds(HapticType type)
        {
            switch (type)
            {
                case HapticType.Selection: return 20;
                case HapticType.LightImpact: return 35;
                case HapticType.MediumImpact: return 60;
                case HapticType.HeavyImpact: return 100;
                case HapticType.Success: return 80;
                case HapticType.Warning: return 120;
                case HapticType.Error: return 180;
                default: return 35;
            }
        }

        public static int GetAndroidAmplitude(HapticType type)
        {
            switch (type)
            {
                case HapticType.Selection: return 60;
                case HapticType.LightImpact: return 90;
                case HapticType.MediumImpact: return 150;
                case HapticType.HeavyImpact: return 255;
                case HapticType.Success: return 170;
                case HapticType.Warning: return 210;
                case HapticType.Error: return 255;
                default: return 90;
            }
        }
    }
}
