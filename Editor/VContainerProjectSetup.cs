#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HP.Framework;
using HP.Framework.Audio;
using HP.Framework.Bootstrap;
using HP.Framework.Haptics;
using HP.Framework.Lifecycle;
using HP.Framework.Pooling;
using HP.Framework.UI;
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

        [MenuItem("Tools/HP Framework/VContainer/Configure Optional Project Integration", false, 10)]
        public static void SetupProjectRoot()
        {
            RootLifetimeScope rootScope = FindBootstrapRootScope();
            if (rootScope == null)
            {
                Debug.LogError(
                    $"[HP Framework/VContainer] Canonical Bootstrap is missing at '{HPFrameworkProjectPaths.BootstrapPath}'. Restore HP Framework from Git.");
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

            VContainerSettings settings = AssetDatabase.LoadAssetAtPath<VContainerSettings>(SettingsPath);
            if (settings == null)
            {
                throw new InvalidOperationException(
                    $"Canonical VContainerSettings is missing at '{SettingsPath}'. Restore HP Framework from Git.");
            }

            // HP Framework uses a manual scene-owned Bootstrap. Keeping this null disables
            // VContainerSettings automatic root prefab instantiation.
            settings.RootLifetimeScope = null;

            List<UnityEngine.Object> preloadedAssets = PlayerSettings.GetPreloadedAssets()
                .Where(asset => asset != null && !(asset is VContainerSettings))
                .ToList();
            preloadedAssets.Add(settings);
            PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            VContainerSettings.LoadInstanceFromPreloadAssets();
            RootLifetimeScopeEditor.EnsureLoadingSceneInBuildSettings();

            Debug.Log(
                $"[HP Framework/VContainer] Optional project integration configured for canonical Bootstrap '{AssetDatabase.GetAssetPath(rootScope)}'. " +
                "The Bootstrap remains scene-owned; VContainerSettings will not auto-instantiate it.");
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

            VContainerSettings canonicalSettings = AssetDatabase.LoadAssetAtPath<VContainerSettings>(SettingsPath);
            if (canonicalSettings == null)
            {
                valid = false;
                Debug.LogError(
                    $"[HP Framework/VContainer] Canonical VContainerSettings is missing at '{SettingsPath}'. Restore HP Framework from Git.");
            }
            else if (canonicalSettings.RootLifetimeScope != null)
            {
                valid = false;
                Debug.LogError(
                    "[HP Framework/VContainer] Canonical VContainerSettings must keep RootLifetimeScope = None. The Bootstrap is scene-owned.");
            }

            if (preloadedSettings.Length > 1)
            {
                valid = false;
                Debug.LogError(
                    $"[HP Framework/VContainer] At most one VContainerSettings asset may be preloaded; found {preloadedSettings.Length}.");
            }

            VContainerSettings settings = preloadedSettings.FirstOrDefault();
            if (settings != null && settings.RootLifetimeScope != null)
            {
                valid = false;
                Debug.LogError(
                    "[HP Framework/VContainer] A preloaded VContainerSettings must not auto-spawn RootLifetimeScope in manual Bootstrap mode.");
            }

            valid = ValidateLoadingSceneSetup() && valid;

            RootLifetimeScope hpRoot = FindBootstrapRootScope();
            if (hpRoot == null)
            {
                valid = false;
                Debug.LogError(
                    "[HP Framework/VContainer] Bootstrap prefab with RootLifetimeScope is missing. Run Tools/HP Framework/Setup / Repair.");
            }
            else
            {
                if (hpRoot.GetComponentInChildren<ApplicationLifecycleService>(true) == null)
                {
                    valid = false;
                    Debug.LogError(
                        "[HP Framework/VContainer] Bootstrap is missing ApplicationLifecycleService. Run Tools/HP Framework/Setup / Repair.");
                }
                AudioManager audioManager = hpRoot.GetComponentInChildren<AudioManager>(true);
                if (audioManager == null)
                {
                    valid = false;
                    Debug.LogError(
                        "[HP Framework/VContainer] Bootstrap is missing AudioManager. Run Tools/HP Framework/Setup / Repair.");
                }
                PoolManager poolManager = hpRoot.GetComponentInChildren<PoolManager>(true);
                if (poolManager == null)
                {
                    valid = false;
                    Debug.LogError(
                        "[HP Framework/VContainer] Bootstrap is missing PoolManager. Run Tools/HP Framework/Setup / Repair.");
                }
                if (hpRoot.GetComponentInChildren<UIManager>(true) == null)
                {
                    valid = false;
                    Debug.LogError(
                        "[HP Framework/VContainer] Bootstrap is missing UIManager. Run Tools/HP Framework/Setup / Repair.");
                }
                if (hpRoot.GetComponentInChildren<GameSceneManager>(true) == null)
                {
                    valid = false;
                    Debug.LogError(
                        "[HP Framework/VContainer] Bootstrap is missing GameSceneManager. Run Tools/HP Framework/Setup / Repair.");
                }
                if (hpRoot.GetComponentInChildren<HapticManager>(true) == null)
                {
                    valid = false;
                    Debug.LogError(
                        "[HP Framework/VContainer] Bootstrap is missing HapticManager. Run Tools/HP Framework/Setup / Repair.");
                }

                ValidateNoDuplicateManager<ApplicationLifecycleService>(hpRoot, ref valid);
                ValidateNoDuplicateManager<AudioManager>(hpRoot, ref valid);
                ValidateNoDuplicateManager<UIManager>(hpRoot, ref valid);
                ValidateNoDuplicateManager<GameSceneManager>(hpRoot, ref valid);
                ValidateNoDuplicateManager<PoolManager>(hpRoot, ref valid);
                ValidateNoDuplicateManager<HapticManager>(hpRoot, ref valid);

                if (poolManager != null)
                {
                    SerializedObject poolObject = new SerializedObject(poolManager);
                    PoolConfigSO poolConfig = poolObject.FindProperty("poolConfig")
                        ?.objectReferenceValue as PoolConfigSO;
                    if (poolConfig != null && !poolConfig.TryValidate(out string poolError))
                    {
                        valid = false;
                        Debug.LogError(
                            $"[HP Framework/VContainer] PoolConfig is invalid:\n{poolError}",
                            poolConfig);
                    }
                }

                SerializedObject rootObject = new SerializedObject(hpRoot);
                MonoBehaviour inputManager = rootObject.FindProperty("inputManager")
                    ?.objectReferenceValue as MonoBehaviour;
                if (inputManager != null)
                {
                    SerializedObject inputObject = new SerializedObject(inputManager);
                    if (inputObject.FindProperty("inputActions")?.objectReferenceValue == null)
                    {
                        valid = false;
                        Debug.LogError(
                            "[HP Framework/VContainer] InputManager has no InputActionAsset assigned. Run Tools/HP Framework/Setup / Repair.",
                            inputManager);
                    }

                    SerializedProperty defaultMap = inputObject.FindProperty("defaultActionMap");
                    if (defaultMap != null && string.IsNullOrWhiteSpace(defaultMap.stringValue))
                    {
                        Debug.LogWarning(
                            "[HP Framework/VContainer] InputManager has no default primary action map. This is valid only when application code selects the primary map explicitly.",
                            inputManager);
                    }
                    else if (defaultMap != null
                             && inputObject.FindProperty("inputActions")
                                 ?.objectReferenceValue is UnityEngine.Object inputActions
                             && !ContainsInputActionMap(inputActions, defaultMap.stringValue))
                    {
                        valid = false;
                        Debug.LogError(
                            $"[HP Framework/VContainer] InputManager default map '{defaultMap.stringValue}' does not exist in '{inputActions.name}'.",
                            inputManager);
                    }
                }
                AudioLibrarySO audioLibrary = rootObject.FindProperty("audioLibrary")
                    ?.objectReferenceValue as AudioLibrarySO;
                if (audioLibrary == null)
                {
                    valid = false;
                    Debug.LogError(
                        "[HP Framework/VContainer] Bootstrap has no AudioLibrary assigned. Run Tools/HP Framework/Setup / Repair.");
                }
                else if (!audioLibrary.TryValidate(out string audioError))
                {
                    valid = false;
                    Debug.LogError(
                        $"[HP Framework/VContainer] AudioLibrary is invalid:\n{audioError}",
                        audioLibrary);
                }
                UICatalogSO uiCatalog = rootObject.FindProperty("uiCatalog")
                    ?.objectReferenceValue as UICatalogSO;
                if (uiCatalog == null)
                {
                    valid = false;
                    Debug.LogError(
                        "[HP Framework/VContainer] Bootstrap has no UICatalog assigned. Run Tools/HP Framework/Setup / Repair.");
                }
                else if (!uiCatalog.TryValidate(out string uiError))
                {
                    valid = false;
                    Debug.LogError(
                        $"[HP Framework/VContainer] UICatalog is invalid:\n{uiError}",
                        uiCatalog);
                }
                if (rootObject.FindProperty("performanceCatalog")?.objectReferenceValue == null)
                {
                    valid = false;
                    Debug.LogError(
                        "[HP Framework/VContainer] Bootstrap has no PerformanceCatalog assigned. Run Tools/HP Framework/Setup / Repair.");
                }
                if (rootObject.FindProperty("devicePerformancePolicy")?.objectReferenceValue == null)
                {
                    valid = false;
                    Debug.LogError(
                        "[HP Framework/VContainer] Bootstrap has no DevicePerformancePolicy assigned. Run Tools/HP Framework/Setup / Repair.");
                }
            }

            ValidateSceneRootDuplicates(settings);
            ValidateFeatureParents();
            ValidateCanonicalAssetDependencies(ref valid);

            if (valid && logSuccess)
            {
                Debug.Log(
                    "[HP Framework/VContainer] Project setup is valid for manual persistent Bootstrap mode.");
            }

            return valid;
        }

        private static bool ValidateLoadingSceneSetup()
        {
            bool valid = true;
            string loadingScenePath = HPFrameworkProjectPaths.LoadingScenePath;
            if (!string.Equals(
                    loadingScenePath,
                    HPFrameworkProjectPaths.LoadingSceneAssetPath,
                    StringComparison.Ordinal))
            {
                valid = false;
                Debug.LogError(
                    "[HP Framework/VContainer] The framework LoadingScene GUID does not resolve to " +
                    $"'{HPFrameworkProjectPaths.LoadingSceneAssetPath}'. Restore the package asset and its .meta file.");
            }
            else if (AssetDatabase.LoadAssetAtPath<SceneAsset>(loadingScenePath) == null)
            {
                valid = false;
                Debug.LogError(
                    $"[HP Framework/VContainer] LoadingScene is missing at '{loadingScenePath}'.");
            }

            EditorBuildSettingsScene loadingBuildScene = EditorBuildSettings.scenes
                .FirstOrDefault(scene => scene.path == loadingScenePath);
            if (loadingBuildScene == null || !loadingBuildScene.enabled)
            {
                valid = false;
                Debug.LogError(
                    "[HP Framework/VContainer] LoadingScene must be present and enabled in Build Settings. " +
                    "Run Tools/HP Framework/Setup to repair it.");
            }

            string bootstrapPath = HPFrameworkProjectPaths.BootstrapPath;
            GameObject bootstrap = AssetDatabase.LoadAssetAtPath<GameObject>(bootstrapPath);
            ApplicationLifecycleService lifecycle = bootstrap != null
                ? bootstrap.GetComponentInChildren<ApplicationLifecycleService>(true)
                : null;
            if (lifecycle == null)
            {
                valid = false;
                Debug.LogError(
                    "[HP Framework/VContainer] Bootstrap does not contain ApplicationLifecycleService. Run Setup to repair it.");
            }

            GameSceneManager sceneManager = bootstrap != null
                ? bootstrap.GetComponentInChildren<GameSceneManager>(true)
                : null;
            if (sceneManager == null)
            {
                valid = false;
                Debug.LogError(
                    "[HP Framework/VContainer] Bootstrap does not contain GameSceneManager. Run Setup to repair it.");
            }
            else
            {
                SerializedObject sceneManagerObject = new SerializedObject(sceneManager);
                string configuredName = sceneManagerObject.FindProperty("loadingSceneName")?.stringValue;
                if (!string.Equals(
                        configuredName,
                        BaseConstants.DefaultLoadingSceneName,
                        StringComparison.Ordinal))
                {
                    valid = false;
                    Debug.LogError(
                        "[HP Framework/VContainer] Bootstrap GameSceneManager must use " +
                        $"'{BaseConstants.DefaultLoadingSceneName}' as its default loading scene.");
                }
            }

            return valid;
        }

        [MenuItem("Tools/HP Framework/VContainer/Open Diagnostics", false, 30)]
        public static void OpenDiagnostics()
        {
            EditorApplication.ExecuteMenuItem("Window/VContainer Diagnostics");
        }

        private static void ValidateCanonicalAssetDependencies(ref bool valid)
        {
            string[] canonicalAssets =
            {
                HPFrameworkProjectPaths.BootstrapPath,
                HPFrameworkProjectPaths.VContainerSettingsPath,
                HPFrameworkProjectPaths.AudioLibraryPath,
                HPFrameworkProjectPaths.UICatalogPath,
                HPFrameworkProjectPaths.LowPerformanceProfilePath,
                HPFrameworkProjectPaths.MediumPerformanceProfilePath,
                HPFrameworkProjectPaths.HighPerformanceProfilePath,
                HPFrameworkProjectPaths.PerformanceCatalogPath,
                HPFrameworkProjectPaths.DevicePerformancePolicyPath
            };

            string frameworkPrefix = HPFrameworkProjectPaths.FrameworkFolder + "/";
            foreach (string assetPath in canonicalAssets)
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
                {
                    valid = false;
                    Debug.LogError(
                        $"[HP Framework] Canonical asset is missing: '{assetPath}'. Restore it and its .meta from Git.");
                    continue;
                }

                string[] dependencies = AssetDatabase.GetDependencies(assetPath, recursive: true);
                foreach (string dependency in dependencies)
                {
                    if (!dependency.StartsWith("Assets/", StringComparison.Ordinal)
                        || dependency.StartsWith(frameworkPrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    valid = false;
                    Debug.LogError(
                        $"[HP Framework] Canonical asset '{assetPath}' depends on host-project asset '{dependency}'. " +
                        "Keep project customization on prefab instances and do not Apply it back to HP Framework.");
                }
            }
        }

        private static void ValidateSceneRootDuplicates(VContainerSettings settings)
        {
            RootLifetimeScope[] sceneRoots = Resources.FindObjectsOfTypeAll<RootLifetimeScope>()
                .Where(scope => scope != null
                    && !EditorUtility.IsPersistent(scope)
                    && scope.gameObject.scene.IsValid()
                    && scope.gameObject.scene.isLoaded)
                .ToArray();

            if (settings != null && settings.RootLifetimeScope != null && sceneRoots.Length > 0)
            {
                Debug.LogWarning(
                    "[HP Framework/VContainer] A project root is already configured through VContainerSettings, " +
                    "but one or more RootLifetimeScope objects also exist in loaded scenes. " +
                    "Prefer scene scopes there to avoid accidentally creating a second application scope.");
                return;
            }

            if (settings != null && settings.RootLifetimeScope == null && sceneRoots.Length > 1)
            {
                Debug.LogWarning(
                    $"[HP Framework/VContainer] Manual Bootstrap mode expects one application RootLifetimeScope, " +
                    $"but {sceneRoots.Length} loaded scene roots were found. Keep Bootstrap only in the entry scene.");
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

        private static void ValidateNoDuplicateManager<T>(
            RootLifetimeScope root,
            ref bool valid)
            where T : Component
        {
            T[] managers = root.GetComponentsInChildren<T>(true);
            if (managers.Length <= 1)
            {
                return;
            }

            valid = false;
            Debug.LogError(
                $"[HP Framework/VContainer] Bootstrap contains {managers.Length} {typeof(T).Name} components. Exactly one owner is allowed.",
                root);
        }

        private static bool ContainsInputActionMap(
            UnityEngine.Object inputActions,
            string mapName)
        {
            MethodInfo findActionMap = inputActions.GetType().GetMethod(
                "FindActionMap",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(string), typeof(bool) },
                modifiers: null);
            return findActionMap == null
                || findActionMap.Invoke(inputActions, new object[] { mapName, false }) != null;
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
            GameObject bootstrap = AssetDatabase.LoadAssetAtPath<GameObject>(
                HPFrameworkProjectPaths.BootstrapPath);
            return bootstrap != null
                ? bootstrap.GetComponent<RootLifetimeScope>()
                : null;
        }
    }
}
#endif
