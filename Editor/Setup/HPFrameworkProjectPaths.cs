using UnityEditor;

namespace HP.Framework.Editor
{
    internal static class HPFrameworkProjectPaths
    {
        public const string FrameworkFolder = "Assets/Plugins/HPFramework";
        public const string DefaultsFolder = FrameworkFolder + "/Runtime/Defaults";
        public const string UIAnimationPresetFolder = DefaultsFolder + "/UIAnimationPresets";
        public const string LoadingSceneAssetPath =
            FrameworkFolder + "/Runtime/Bootstrap/Loading/Scenes/LoadingScene.unity";
        public const string PerformanceFolder = DefaultsFolder + "/Performance";

        public const string BootstrapPath =
            FrameworkFolder + "/Runtime/Bootstrap/Prefabs/Bootstrap.prefab";
        public const string VContainerSettingsPath = DefaultsFolder + "/VContainerSettings.asset";
        public const string AudioLibraryPath = DefaultsFolder + "/DefaultAudioLibrary.asset";
        public const string UICatalogPath = DefaultsFolder + "/DefaultUICatalog.asset";
        public const string LowPerformanceProfilePath = PerformanceFolder + "/Low.asset";
        public const string MediumPerformanceProfilePath = PerformanceFolder + "/Medium.asset";
        public const string HighPerformanceProfilePath = PerformanceFolder + "/High.asset";
        public const string PerformanceCatalogPath = PerformanceFolder + "/PerformanceCatalog.asset";
        public const string DevicePerformancePolicyPath = PerformanceFolder + "/DevicePerformancePolicy.asset";

        public const string BootstrapGuid = "550d92965c5c4c53b9949039d465faba";
        public const string LoadingSceneGuid = "9806de10e37b6b04092877dc41f8724d";
        public const string ToastPrefabGuid = "ba829f6fed5fb0f4fa2a7abf5f8b87fa";
        public const string DefaultInputActionsGuid = "1b507b5bc5a885343a365d6380c49c18";

        public static string CanonicalBootstrapPath =>
            AssetDatabase.GUIDToAssetPath(BootstrapGuid);

        public static string LoadingScenePath =>
            AssetDatabase.GUIDToAssetPath(LoadingSceneGuid);

        public static string ToastPrefabPath =>
            AssetDatabase.GUIDToAssetPath(ToastPrefabGuid);

        public static string DefaultInputActionsPath =>
            AssetDatabase.GUIDToAssetPath(DefaultInputActionsGuid);
    }
}
