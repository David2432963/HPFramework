#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HP.Framework.Bootstrap;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;

namespace HP.Framework.Editor
{
    /// <summary>
    /// Project-level VContainer setup and validation. Keeps VContainer itself visible to game
    /// code while removing the repetitive editor setup needed by every project.
    /// </summary>
    public static class VContainerProjectSetup
    {
        public static string SettingsPath => HPFrameworkProjectPaths.VContainerSettingsPath;

        [MenuItem("Tools/HP Framework/VContainer/Setup Manual Bootstrap Mode", false, 10)]
        public static void SetupProjectRoot()
        {
            RootLifetimeScope rootScope = FindBootstrapRootScope()
                ?? BootstrapPrefabCreator.CreateOrRepairBootstrap(configureProjectRoot: false);
            if (rootScope == null)
            {
                Debug.LogError(
                    "[HP Framework/VContainer] Could not create or find a Bootstrap prefab containing RootLifetimeScope.");
                return;
            }

            ConfigureProjectRoot(rootScope);
        }

        public static VContainerSettings ConfigureProjectRoot(RootLifetimeScope rootScope)
        {
            if (rootScope == null)
            {
                throw new ArgumentNullException(nameof(rootScope));
            }

            VContainerSettings settings = FindOrCreateSettings(rootScope, out bool createdSettings);
            // HP Framework uses a manual scene-owned Bootstrap. Keeping this null disables
            // VContainerSettings automatic root prefab instantiation.
            settings.RootLifetimeScope = null;
            // Diagnostics are opt-in for newly created settings, but Repair must preserve a
            // project's explicit diagnostics choice on an existing VContainerSettings asset.
            if (createdSettings)
            {
                settings.EnableDiagnostics = false;
            }

            List<UnityEngine.Object> preloadedAssets = PlayerSettings.GetPreloadedAssets()
                .Where(asset => asset != null && !(asset is VContainerSettings))
                .ToList();
            preloadedAssets.Add(settings);
            PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            VContainerSettings.LoadInstanceFromPreloadAssets();

            Debug.Log(
                $"[HP Framework/VContainer] Manual Bootstrap configured: {AssetDatabase.GetAssetPath(rootScope)}. " +
                $"VContainerSettings will not auto-instantiate it; add the Bootstrap prefab to each scene that needs HP Framework.");
            return settings;
        }

        [MenuItem("Tools/HP Framework/VContainer/Validate Project Setup", false, 11)]
        public static void ValidateProjectSetupMenu()
        {
            ValidateProjectSetup(logSuccess: true);
        }

        public static bool ValidateProjectSetup(bool logSuccess)
        {
            bool valid = true;
            VContainerSettings[] preloadedSettings = PlayerSettings.GetPreloadedAssets()
                .OfType<VContainerSettings>()
                .ToArray();

            if (preloadedSettings.Length != 1)
            {
                valid = false;
                Debug.LogError(
                    $"[HP Framework/VContainer] Expected exactly one preloaded VContainerSettings asset, " +
                    $"found {preloadedSettings.Length}. Run Tools/HP Framework/Setup.");
            }

            VContainerSettings settings = preloadedSettings.FirstOrDefault();
            if (settings == null)
            {
                valid = false;
                Debug.LogError(
                    "[HP Framework/VContainer] Preloaded VContainerSettings is missing.");
            }
            else if (settings.RootLifetimeScope != null)
            {
                valid = false;
                Debug.LogError(
                    "[HP Framework/VContainer] Automatic RootLifetimeScope spawning is disabled for this project. " +
                    "Clear VContainerSettings.RootLifetimeScope and place the HP Bootstrap prefab explicitly in scenes that need it.");
            }

            ValidateSceneRootDuplicates(settings);
            ValidateFeatureParents();

            if (valid && logSuccess)
            {
                Debug.Log(
                    "[HP Framework/VContainer] Project setup is valid for manual scene-owned Bootstrap mode.");
            }

            return valid;
        }

        [MenuItem("Tools/HP Framework/VContainer/Open Diagnostics", false, 30)]
        public static void OpenDiagnostics()
        {
            EditorApplication.ExecuteMenuItem("Window/VContainer Diagnostics");
        }

        private static void ValidateSceneRootDuplicates(VContainerSettings settings)
        {
            if (settings == null || settings.RootLifetimeScope == null)
            {
                return;
            }

            RootLifetimeScope[] sceneRoots = Resources.FindObjectsOfTypeAll<RootLifetimeScope>()
                .Where(scope => scope != null
                    && !EditorUtility.IsPersistent(scope)
                    && scope.gameObject.scene.IsValid()
                    && scope.gameObject.scene.isLoaded)
                .ToArray();

            if (sceneRoots.Length > 0)
            {
                Debug.LogWarning(
                    "[HP Framework/VContainer] A project root is already configured through VContainerSettings, " +
                    "but one or more RootLifetimeScope objects also exist in loaded scenes. " +
                    "Prefer scene scopes there to avoid accidentally creating a second application scope.");
            }
        }

        private static void ValidateFeatureParents()
        {
            BaseFeatureLifetimeScope[] featureScopes = Resources
                .FindObjectsOfTypeAll<BaseFeatureLifetimeScope>()
                .Where(scope => scope != null
                    && !EditorUtility.IsPersistent(scope)
                    && scope.gameObject.scene.IsValid()
                    && scope.gameObject.scene.isLoaded)
                .ToArray();

            foreach (BaseFeatureLifetimeScope featureScope in featureScopes)
            {
                if (HasExplicitOrHierarchyParent(featureScope))
                {
                    continue;
                }

                Debug.LogWarning(
                    $"[HP Framework/VContainer] Feature scope '{featureScope.name}' has no explicit or hierarchy " +
                    "LifetimeScope parent. It will fall back to the project root and may bypass scene-scoped services.",
                    featureScope);
            }
        }

        private static bool HasExplicitOrHierarchyParent(LifetimeScope scope)
        {
            if (scope.parentReference.Object != null
                || !string.IsNullOrWhiteSpace(scope.parentReference.TypeName))
            {
                return true;
            }

            Transform current = scope.transform.parent;
            while (current != null)
            {
                if (current.TryGetComponent(out LifetimeScope _))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static RootLifetimeScope FindBootstrapRootScope()
        {
            GameObject projectBootstrap = AssetDatabase.LoadAssetAtPath<GameObject>(
                HPFrameworkProjectPaths.BootstrapPath);
            RootLifetimeScope projectScope = projectBootstrap != null
                ? projectBootstrap.GetComponent<RootLifetimeScope>()
                : null;
            if (projectScope != null)
            {
                return projectScope;
            }

            string[] guids = AssetDatabase.FindAssets("Bootstrap t:Prefab");
            RootLifetimeScope packageFallback = null;
            RootLifetimeScope fallback = null;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                RootLifetimeScope scope = prefab != null
                    ? prefab.GetComponent<RootLifetimeScope>()
                    : null;
                if (scope == null)
                {
                    continue;
                }

                // A project-owned Bootstrap may derive from RootLifetimeScope and register
                // game-specific services, so always prefer Assets over the package default.
                if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    return scope;
                }

                if (path.IndexOf("com.hp.framework", StringComparison.OrdinalIgnoreCase) >= 0
                    || string.Equals(
                        guid,
                        HPFrameworkProjectPaths.BootstrapTemplateGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    packageFallback ??= scope;
                }

                fallback ??= scope;
            }

            return packageFallback ?? fallback;
        }

        private static VContainerSettings FindOrCreateSettings(
            RootLifetimeScope rootScope,
            out bool created)
        {
            created = false;
            VContainerSettings[] preloadedSettings = PlayerSettings.GetPreloadedAssets()
                .OfType<VContainerSettings>()
                .Where(settings => settings != null)
                .ToArray();

            if (preloadedSettings.Length == 1)
            {
                return preloadedSettings[0];
            }

            if (preloadedSettings.Length > 1)
            {
                VContainerSettings matchingRoot = preloadedSettings.FirstOrDefault(
                    settings => settings.RootLifetimeScope == rootScope);
                if (matchingRoot != null)
                {
                    return matchingRoot;
                }

                VContainerSettings canonicalPreloaded = preloadedSettings.FirstOrDefault(
                    settings => string.Equals(
                        AssetDatabase.GetAssetPath(settings),
                        SettingsPath,
                        StringComparison.OrdinalIgnoreCase));
                if (canonicalPreloaded != null)
                {
                    return canonicalPreloaded;
                }
            }

            VContainerSettings canonical = AssetDatabase.LoadAssetAtPath<VContainerSettings>(
                SettingsPath);
            if (canonical != null)
            {
                return canonical;
            }

            string[] guids = AssetDatabase.FindAssets("t:VContainerSettings");
            if (guids.Length == 1)
            {
                string onlyPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                VContainerSettings onlySettings =
                    AssetDatabase.LoadAssetAtPath<VContainerSettings>(onlyPath);
                if (onlySettings != null)
                {
                    return onlySettings;
                }
            }
            else if (guids.Length > 1)
            {
                Debug.LogWarning(
                    $"[HP Framework/VContainer] Found multiple VContainerSettings assets. " +
                    $"Setup will create/use the deterministic framework settings at '{SettingsPath}' " +
                    "instead of selecting an arbitrary asset.");
            }

            EnsureFolder(HPFrameworkProjectPaths.SettingsFolder);
            VContainerSettings settings = ScriptableObject.CreateInstance<VContainerSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            created = true;
            return settings;
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Replace('\\', '/').Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }
    }
}
#endif


