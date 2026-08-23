using System;
using HP.Framework.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace HP.Framework.Editor
{
    public static class BootstrapPrefabCreator
    {
        public static string BootstrapPath => HPFrameworkProjectPaths.BootstrapPath;

        [MenuItem("Tools/HP Framework/Repair Canonical Bootstrap", false, 20)]
        public static void CreateBootstrapPrefab()
        {
            CreateOrRepairBootstrap(configureProjectRoot: true);
        }

        /// <summary>
        /// Repairs the Git-tracked canonical Bootstrap in place. The framework no longer generates
        /// a host-project Bootstrap copy; consuming projects customize prefab instances through
        /// scene overrides instead of applying project references back to the framework asset.
        /// </summary>
        public static RootLifetimeScope CreateOrRepairBootstrap(
            bool configureProjectRoot,
            bool resetDefaults = false)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapPath) == null)
            {
                throw new InvalidOperationException(
                    $"Canonical HP Framework Bootstrap is missing at '{BootstrapPath}'. " +
                    "Restore the framework asset and its .meta file from Git.");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(BootstrapPath);
            try
            {
                RootLifetimeScope scope = root.GetComponent<RootLifetimeScope>()
                    ?? root.AddComponent<RootLifetimeScope>();
                RootLifetimeScopeEditor.AutoSetupHierarchy(scope, resetDefaults);
                RootLifetimeScopeEditor.AutoSetupScriptableObjects(scope);
                RootLifetimeScopeEditor.AutoSetupDefaultRuntimeAssets(scope);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BootstrapPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Unity failed to save canonical Bootstrap at '{BootstrapPath}'.");
                }

                RootLifetimeScope prefabScope = prefab.GetComponent<RootLifetimeScope>();
                if (configureProjectRoot && prefabScope != null)
                {
                    VContainerProjectSetup.ConfigureProjectRoot(prefabScope);
                }

                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                AssetDatabase.SaveAssets();
                Debug.Log($"[HP Framework] Canonical Bootstrap repaired at {BootstrapPath}");
                return prefabScope;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}

