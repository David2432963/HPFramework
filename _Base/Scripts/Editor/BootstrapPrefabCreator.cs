using System;
using UnityEditor;
using UnityEngine;
using Base.Bootstrap;

namespace Base.Editor
{
    public static class BootstrapPrefabCreator
    {
        [MenuItem("Base/Create Bootstrap Prefab", false, 10)]
        public static void CreateBootstrapPrefab()
        {
            const string prefabDirectory = "Assets/_Base/Prefabs";
            const string prefabPath = prefabDirectory + "/Bootstrap.prefab";

            EnsureFolder(prefabDirectory);

            GameObject root = new GameObject("Bootstrap");
            try
            {
                RootLifetimeScope scope = root.AddComponent<RootLifetimeScope>();
                RootLifetimeScopeEditor.AutoSetupHierarchy(scope);
                RootLifetimeScopeEditor.AutoSetupScriptableObjects(scope);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Unity failed to save Bootstrap prefab at '{prefabPath}'.");
                }

                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[Bootstrap] Created a valid prefab at {prefabPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
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
