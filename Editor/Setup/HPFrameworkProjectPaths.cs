using UnityEditor;

namespace HP.Framework.Editor
{
    internal static class HPFrameworkProjectPaths
    {
        public const string FrameworkFolder = "Assets/Plugins/HPFramework";
        public const string GeneratedFolder = FrameworkFolder + "/Generated";
        public const string SettingsFolder = FrameworkFolder + "/Settings";
        public const string UIAnimationPresetFolder = SettingsFolder + "/UIAnimationPresets";

        public const string BootstrapPath = GeneratedFolder + "/Bootstrap.prefab";
        public const string VContainerSettingsPath = SettingsFolder + "/VContainerSettings.asset";
        public const string AudioLibraryPath = SettingsFolder + "/DefaultAudioLibrary.asset";
        public const string UICatalogPath = SettingsFolder + "/DefaultUICatalog.asset";

        public const string BootstrapTemplateGuid = "550d92965c5c4c53b9949039d465faba";
        public const string ToastPrefabGuid = "ba829f6fed5fb0f4fa2a7abf5f8b87fa";
        public const string DefaultInputActionsGuid = "1b507b5bc5a885343a365d6380c49c18";

        public static string BootstrapTemplatePath =>
            AssetDatabase.GUIDToAssetPath(BootstrapTemplateGuid);

        public static string ToastPrefabPath =>
            AssetDatabase.GUIDToAssetPath(ToastPrefabGuid);

        public static string DefaultInputActionsPath =>
            AssetDatabase.GUIDToAssetPath(DefaultInputActionsGuid);
    }
}
