#if UNITY_EDITOR
using System;
using System.Linq;
using HP.Framework.Lifecycle;
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
                "HP Framework ships a Git-tracked, fully configured Bootstrap and framework defaults. " +
                "Projects use prefab-instance overrides; Setup is optional validation/repair, not asset generation.",
                MessageType.Info);

            EditorGUILayout.Space(8f);
            GameObject bootstrapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                HPFrameworkProjectPaths.BootstrapPath);
            DrawStatus("Bootstrap prefab", bootstrapPrefab != null);
            DrawStatus("Application lifecycle", bootstrapPrefab != null
                && bootstrapPrefab.GetComponentInChildren<ApplicationLifecycleService>(true) != null);
            DrawStatus("Runtime manager ownership", bootstrapPrefab != null
                && bootstrapPrefab.GetComponentsInChildren<MonoBehaviour>(true).Length > 0);
            DrawStatus("VContainer settings", AssetDatabase.LoadAssetAtPath<VContainerSettings>(
                HPFrameworkProjectPaths.VContainerSettingsPath) != null);

            DrawStatus("Default AudioLibrary", AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                HPFrameworkProjectPaths.AudioLibraryPath) != null);
            DrawStatus("Default UICatalog", AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                HPFrameworkProjectPaths.UICatalogPath) != null);
            DrawStatus("Default PerformanceCatalog", AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                HPFrameworkProjectPaths.PerformanceCatalogPath) != null);

            VContainerSettings[] preloadedSettings = PlayerSettings.GetPreloadedAssets()
                .OfType<VContainerSettings>()
                .ToArray();
            DrawStatus("VContainer preload compatible (optional)", preloadedSettings.Length <= 1);
            DrawStatus("Canonical Bootstrap GUID", string.Equals(
                HPFrameworkProjectPaths.CanonicalBootstrapPath,
                HPFrameworkProjectPaths.BootstrapPath,
                StringComparison.Ordinal));
            string loadingScenePath = HPFrameworkProjectPaths.LoadingScenePath;
            DrawStatus("Framework LoadingScene",
                string.Equals(
                    loadingScenePath,
                    HPFrameworkProjectPaths.LoadingSceneAssetPath,
                    StringComparison.Ordinal)
                && AssetDatabase.LoadAssetAtPath<SceneAsset>(loadingScenePath) != null);
            DrawStatus("LoadingScene in Build Settings",
                EditorBuildSettings.scenes.Any(scene =>
                    scene.path == loadingScenePath && scene.enabled));
            bool inputSystemAvailable = Type.GetType(
                "UnityEngine.InputSystem.InputActionAsset, Unity.InputSystem",
                throwOnError: false) != null;
            DrawStatus("Unity Input System", inputSystemAvailable);
            DrawStatus("Default UI input actions",
                inputSystemAvailable
                && !string.IsNullOrWhiteSpace(HPFrameworkProjectPaths.DefaultInputActionsPath));
            DrawStatus("Default Toast prefab",
                !string.IsNullOrWhiteSpace(HPFrameworkProjectPaths.ToastPrefabPath));

            EditorGUILayout.HelpBox(
                "Async startup is registered by Bootstrap but starts explicitly from the application boot flow. " +
                "Register IStartupTask implementations in a derived RootLifetimeScope.RegisterApplicationStartupTasks override.",
                MessageType.None);
            EditorGUILayout.HelpBox(
                "Validate Project checks manager ownership, Input Actions, performance references, pool budgets, and Audio/UI catalog policy without overwriting custom configuration.",
                MessageType.None);

            EditorGUILayout.Space(12f);
            if (GUILayout.Button("Repair Canonical Framework Defaults", GUILayout.Height(34f)))
            {
                BootstrapPrefabCreator.CreateOrRepairBootstrap(
                    configureProjectRoot: true,
                    resetDefaults: false);
                Repaint();
            }

            EditorGUILayout.HelpBox(
                "Scene prefab instances keep their own overrides. Inspector Repair only fills missing references and preserves valid project overrides. " +
                "Use Reset here only when intentionally restoring the canonical framework prefab itself.",
                MessageType.None);

            if (GUILayout.Button("Reset Canonical Bootstrap To Framework Defaults", GUILayout.Height(26f)))
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
