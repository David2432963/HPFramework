#if UNITY_EDITOR
using HP.Framework.UI;
using UnityEditor;
using UnityEngine;

namespace HP.Framework.Editor.UI
{
    public static class UIAnimationPresetGenerator
    {
        private static string FolderPath => HPFrameworkProjectPaths.UIAnimationPresetFolder;
        private static string AssetPath => FolderPath + "/UIAnim_PopupFadeScale.asset";

        [MenuItem("Tools/HP Framework/UI/Create Default Popup Fade Scale Preset", false, 40)]
        public static void CreateDefaultPopupPreset()
        {
            EnsureFolder(HPFrameworkProjectPaths.SettingsFolder, "UIAnimationPresets");

            UIAnimationPresetSO preset =
                AssetDatabase.LoadAssetAtPath<UIAnimationPresetSO>(AssetPath);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<UIAnimationPresetSO>();
                AssetDatabase.CreateAsset(preset, AssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[UI] Created animation preset at {AssetPath}.");
            }
            else
            {
                Debug.Log(
                    $"[UI] Animation preset already exists at {AssetPath}. Existing values were preserved.");
            }

            Selection.activeObject = preset;
            EditorGUIUtility.PingObject(preset);
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
#endif



