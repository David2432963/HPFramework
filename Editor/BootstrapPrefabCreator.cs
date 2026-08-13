using System;
using HP.Framework.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace HP.Framework.Editor
{
    public static class BootstrapPrefabCreator
    {
        public static string BootstrapPath => HPFrameworkProjectPaths.BootstrapPath;

        [MenuItem("Tools/HP Framework/Create Bootstrap Prefab", false, 20)]
        public static void CreateBootstrapPrefab()
        {
            CreateOrRepairBootstrap(configureProjectRoot: true);
        }

        public static RootLifetimeScope CreateOrRepairBootstrap(
            bool configureProjectRoot,
            bool resetDefaults = false)
        {
            EnsureFolder(HPFrameworkProjectPaths.GeneratedFolder);

            bool hasExistingBootstrap =
                AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapPath) != null;
            string sourcePath = hasExistingBootstrap
                ? BootstrapPath
                : HPFrameworkProjectPaths.BootstrapTemplatePath;

            GameObject root = null;
            bool loadedPrefabContents = false;
            try
            {
                if (!string.IsNullOrWhiteSpace(sourcePath)
                    && AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) != null)
                {
                    root = PrefabUtility.LoadPrefabContents(sourcePath);
                    loadedPrefabContents = true;
                }
                else
                {
                    root = new GameObject("Bootstrap");
                }

                RootLifetimeScope scope = root.GetComponent<RootLifetimeScope>()
                    ?? root.AddComponent<RootLifetimeScope>();
                RootLifetimeScopeEditor.AutoSetupHierarchy(
                    scope,
                    resetDefaults || !hasExistingBootstrap);
                RootLifetimeScopeEditor.AutoSetupScriptableObjects(scope);
                RootLifetimeScopeEditor.AutoSetupDefaultRuntimeAssets(scope);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BootstrapPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Unity failed to save Bootstrap prefab at '{BootstrapPath}'.");
                }

                RootLifetimeScope prefabScope = prefab.GetComponent<RootLifetimeScope>();
                if (configureProjectRoot && prefabScope != null)
                {
                    VContainerProjectSetup.ConfigureProjectRoot(prefabScope);
                }

                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                AssetDatabase.SaveAssets();
                Debug.Log($"[HP Framework] Bootstrap ready at {BootstrapPath}");
                return prefabScope;
            }
            finally
            {
                if (root != null)
                {
                    if (loadedPrefabContents)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(root);
                    }
                }
            }
        }

        private static void EnsureFolder(string path)
        {
            string normalized = path.Replace('\\', '/');
            string[] segments = normalized.Split('/');
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

