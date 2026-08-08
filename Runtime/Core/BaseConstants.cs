namespace HP.Framework
{
    /// <summary>
    /// Centralized constants used across HP Framework.
    /// Project-specific constants should NOT be added here.
    /// </summary>
    public static class BaseConstants
    {
        // Persistence
        public const string SettingsSection = "Settings";
        public const string HapticPrefsKey = "settings_haptic_enabled";
        public const string PlayerPrefSeparator = ".";
        public const string JsonFileExtension = ".json";

        // Scene Names
        public const string DefaultLoadingSceneName = "LoadingScene";

        // Audio
        public const string ButtonClickAudioKey = "button_click";

        // Camera / Rendering
        public const float Bounds2DDepth = 100000f;
    }
}




