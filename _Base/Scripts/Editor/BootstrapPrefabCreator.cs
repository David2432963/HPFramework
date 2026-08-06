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
            string prefabPath = "Assets/_Base/Prefabs/Bootstrap.prefab";

            // Create a temporary GameObject
            GameObject go = new GameObject("Bootstrap");
            
            // Add RootLifetimeScope (this acts as the core of the Bootstrap)
            var scope = go.AddComponent<RootLifetimeScope>();
            
            // Run the auto-setup methods to populate managers and children (Camera, Canvas, etc.)
            scope.AutoSetupHierarchy();
            scope.AutoSetupScriptableObjects();

            // Ensure the Prefabs folder exists
            if (!AssetDatabase.IsValidFolder("Assets/_Base/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets/_Base", "Prefabs");
            }

            // Save as Prefab
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            
            // Destroy the temporary GameObject
            GameObject.DestroyImmediate(go);
            
            Debug.Log($"[Bootstrap] Successfully created Bootstrap prefab at {prefabPath}");
            AssetDatabase.Refresh();
        }
    }
}
