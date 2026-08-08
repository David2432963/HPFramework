#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;

namespace HP.Framework.Editor
{
    public sealed class HPFrameworkSetupWindow : EditorWindow
    {
        [MenuItem("Tools/HP Framework/Setup", false, 0)]
        public static void Open()
        {
            HPFrameworkSetupWindow window = GetWindow<HPFrameworkSetupWindow>(
                utility: false,
                title: "HP Framework Setup",
                focus: true);
            window.minSize = new Vector2(440f, 360f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("HP Framework", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Setup keeps Bootstrap and default settings inside Assets/Plugins/HPFramework " +
                "so the complete framework remains editable and versioned together during development.",
                MessageType.Info);

            EditorGUILayout.Space(8f);
            DrawStatus("Bootstrap prefab", AssetDatabase.LoadAssetAtPath<GameObject>(
                HPFrameworkProjectPaths.BootstrapPath) != null);
            DrawStatus("VContainer settings", AssetDatabase.LoadAssetAtPath<VContainerSettings>(
                HPFrameworkProjectPaths.VContainerSettingsPath) != null);

            VContainerSettings[] preloadedSettings = PlayerSettings.GetPreloadedAssets()
                .OfType<VContainerSettings>()
                .ToArray();
            DrawStatus("VContainer preloaded asset", preloadedSettings.Length == 1);
            DrawStatus("Package Bootstrap template",
                !string.IsNullOrWhiteSpace(HPFrameworkProjectPaths.BootstrapTemplatePath));
            bool inputSystemAvailable = Type.GetType(
                "UnityEngine.InputSystem.InputActionAsset, Unity.InputSystem",
                throwOnError: false) != null;
            DrawStatus("Unity Input System (optional)", inputSystemAvailable);
            DrawStatus("Default UI input actions",
                inputSystemAvailable
                && !string.IsNullOrWhiteSpace(HPFrameworkProjectPaths.DefaultInputActionsPath));
            DrawStatus("Default Toast prefab",
                !string.IsNullOrWhiteSpace(HPFrameworkProjectPaths.ToastPrefabPath));

            EditorGUILayout.Space(12f);
            if (GUILayout.Button("Setup / Repair Missing References", GUILayout.Height(34f)))
            {
                BootstrapPrefabCreator.CreateOrRepairBootstrap(
                    configureProjectRoot: true,
                    resetDefaults: false);
                Repaint();
            }

            EditorGUILayout.HelpBox(
                "Repair preserves existing camera, Canvas Scaler, layout, catalogs, Input Actions, and Toast customizations. " +
                "Use Reset only when you explicitly want HP Framework defaults reapplied.",
                MessageType.None);

            if (GUILayout.Button("Reset Bootstrap To Framework Defaults", GUILayout.Height(26f)))
            {
                if (EditorUtility.DisplayDialog(
                        "Reset HP Framework Bootstrap",
                        "This reapplies HP Framework camera, canvas and layout defaults. Continue?",
                        "Reset",
                        "Cancel"))
                {
                    BootstrapPrefabCreator.CreateOrRepairBootstrap(
                        configureProjectRoot: true,
                        resetDefaults: true);
                    Repaint();
                }
            }

            if (GUILayout.Button("Validate Project", GUILayout.Height(28f)))
            {
                VContainerProjectSetup.ValidateProjectSetup(logSuccess: true);
                Repaint();
            }

            if (GUILayout.Button("Open VContainer Diagnostics", GUILayout.Height(24f)))
            {
                VContainerProjectSetup.OpenDiagnostics();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Framework assets: " + HPFrameworkProjectPaths.FrameworkFolder,
                MessageType.None);
        }

        private static void DrawStatus(string label, bool valid)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(valid ? "✓" : "○", GUILayout.Width(20f));
            EditorGUILayout.LabelField(label);
            GUILayout.FlexibleSpace();
            GUILayout.Label(valid ? "Ready" : "Missing", GUILayout.Width(60f));
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
