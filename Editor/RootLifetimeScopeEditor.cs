#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using HP.Framework.Audio;
using HP.Framework.Bootstrap;
using HP.Framework.Haptics;
using HP.Framework.Pooling;
using HP.Framework.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HP.Framework.Editor
{
    [CustomEditor(typeof(RootLifetimeScope), true)]
    public class RootLifetimeScopeEditor : UnityEditor.Editor
    {
        private const string InputManagerAssemblyQualifiedName =
            "HP.Framework.Input.InputManager, HP.Framework.Input";
        public override void OnInspectorGUI()
        {
            RootLifetimeScope scope = (RootLifetimeScope)target;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("HP Framework Root", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "RootLifetimeScope is the application composition root. Keep only app-lifetime " +
                "dependencies here; put gameplay/menu state in scene or feature scopes.",
                MessageType.Info);

            if (GUILayout.Button("Repair Missing Hierarchy & References", GUILayout.Height(30f)))
            {
                AutoSetupHierarchy(scope, resetDefaults: false);
            }

            if (GUILayout.Button("Reset Hierarchy To Framework Defaults", GUILayout.Height(24f))
                && EditorUtility.DisplayDialog(
                    "Reset HP Framework Bootstrap",
                    "This reapplies HP Framework camera, canvas and layout defaults. Continue?",
                    "Reset",
                    "Cancel"))
            {
                AutoSetupHierarchy(scope, resetDefaults: true);
            }

            if (GUILayout.Button("Find / Create Project Catalogs", GUILayout.Height(26f)))
            {
                AutoSetupScriptableObjects(scope);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("VContainer Project Root", EditorStyles.boldLabel);
            if (GUILayout.Button("Configure VContainerSettings", GUILayout.Height(26f)))
            {
                if (EditorUtility.IsPersistent(scope))
                {
                    VContainerProjectSetup.ConfigureProjectRoot(scope);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Save this RootLifetimeScope as a prefab before configuring it as the project root.",
                        MessageType.Warning);
                    Debug.LogWarning(
                        "[HP Framework/VContainer] RootLifetimeScope must be a prefab asset before it can be assigned to VContainerSettings.",
                        scope);
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate Setup"))
            {
                VContainerProjectSetup.ValidateProjectSetup(logSuccess: true);
            }
            if (GUILayout.Button("Open Diagnostics"))
            {
                VContainerProjectSetup.OpenDiagnostics();
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Add Current & Loading Scene To Build Settings"))
            {
                AutoSetupBuildSettings();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
            DrawDefaultInspector();
        }

        public static void AutoSetupHierarchy(
            RootLifetimeScope scope,
            bool resetDefaults = false)
        {
            GameObject rootGo = scope.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(rootGo, "Auto Setup Bootstrap Hierarchy");

            AudioManager audioManager = GetOrAddComponent<AudioManager>(rootGo);
            UIManager uiManager = GetOrAddComponent<UIManager>(rootGo);
            MonoBehaviour inputManager = GetOrAddOptionalMonoBehaviour(
                rootGo,
                InputManagerAssemblyQualifiedName);
            GameSceneManager sceneManager = GetOrAddComponent<GameSceneManager>(rootGo);
            PoolManager poolManager = GetOrAddComponent<PoolManager>(rootGo);
            HapticManager hapticManager = GetOrAddComponent<HapticManager>(rootGo);

            bool cameraCreated = rootGo.transform.Find("UICamera") == null;
            Transform cameraTransform = FindOrCreateChild(rootGo.transform, "UICamera");
            bool cameraComponentCreated = !cameraTransform.TryGetComponent(out Camera uiCamera);
            uiCamera ??= GetOrAddComponent<Camera>(cameraTransform.gameObject);
            if (resetDefaults || cameraCreated || cameraComponentCreated)
            {
                uiCamera.orthographic = true;
                uiCamera.orthographicSize = 5f;
                uiCamera.nearClipPlane = 0.3f;
                uiCamera.farClipPlane = 1000f;
                uiCamera.clearFlags = CameraClearFlags.SolidColor;
                uiCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                uiCamera.cullingMask = 1 << 5;
            }

            URPCameraStackUtility.ConfigureAsOverlay(uiCamera);

            bool canvasCreated = rootGo.transform.Find("UICanvas") == null;
            Transform canvasTransform = FindOrCreateChild(rootGo.transform, "UICanvas");
            bool canvasComponentCreated = !canvasTransform.TryGetComponent(out Canvas canvas);
            canvas ??= GetOrAddComponent<Canvas>(canvasTransform.gameObject);
            // Adding Canvas can replace a plain Transform with a RectTransform. Always reacquire
            // the live transform before creating/scanning child UI objects.
            canvasTransform = canvas.transform;
            if (resetDefaults || canvasCreated || canvasComponentCreated)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCamera;
                canvas.planeDistance = 100f;
            }
            else if (canvas.worldCamera == null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvas.worldCamera = uiCamera;
            }

            bool scalerCreated = !canvasTransform.TryGetComponent(out CanvasScaler scaler);
            scaler ??= GetOrAddComponent<CanvasScaler>(canvasTransform.gameObject);
            if (resetDefaults || canvasCreated || scalerCreated)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
            GetOrAddComponent<GraphicRaycaster>(canvasTransform.gameObject);

            bool screenCreated = canvasTransform.Find("ScreenCanvas") == null;
            bool popupCreated = canvasTransform.Find("PopupCanvas") == null;
            bool notificationCreated = canvasTransform.Find("NotiCanvas") == null;
            bool lockCreated = canvasTransform.Find("LockCanvas") == null;
            RectTransform screenCanvas = FindOrCreateChildRect(canvasTransform, "ScreenCanvas");
            RectTransform popupCanvas = FindOrCreateChildRect(canvasTransform, "PopupCanvas");
            RectTransform notificationCanvas = FindOrCreateChildRect(canvasTransform, "NotiCanvas");
            RectTransform lockCanvas = FindOrCreateChildRect(canvasTransform, "LockCanvas");
            if (resetDefaults || screenCreated) SetFullStretch(screenCanvas);
            if (resetDefaults || popupCreated) SetFullStretch(popupCanvas);
            if (resetDefaults || notificationCreated) SetFullStretch(notificationCanvas);
            if (resetDefaults || lockCreated) SetFullStretch(lockCanvas);

            GetOrAddComponent<CanvasGroup>(lockCanvas.gameObject);
            bool lockImageCreated = !lockCanvas.TryGetComponent(out Image lockImage);
            lockImage ??= GetOrAddComponent<Image>(lockCanvas.gameObject);
            if (resetDefaults || lockCreated || lockImageCreated)
            {
                lockImage.color = new Color(0f, 0f, 0f, 0f);
                lockCanvas.gameObject.SetActive(false);
            }

            Transform poolRoot = FindOrCreateChild(rootGo.transform, "PoolRoot");

            SerializedObject scopeObject = new SerializedObject(scope);
            scopeObject.FindProperty("audioManager").objectReferenceValue = audioManager;
            scopeObject.FindProperty("uiManager").objectReferenceValue = uiManager;
            if (inputManager != null)
            {
                scopeObject.FindProperty("inputManager").objectReferenceValue = inputManager;
            }
            scopeObject.FindProperty("gameSceneManager").objectReferenceValue = sceneManager;
            scopeObject.FindProperty("poolManager").objectReferenceValue = poolManager;
            scopeObject.FindProperty("hapticManager").objectReferenceValue = hapticManager;
            scopeObject.ApplyModifiedProperties();

            SerializedObject uiObject = new SerializedObject(uiManager);
            uiObject.FindProperty("screenCanvas").objectReferenceValue = screenCanvas;
            uiObject.FindProperty("popupCanvas").objectReferenceValue = popupCanvas;
            uiObject.FindProperty("notiCanvas").objectReferenceValue = notificationCanvas;
            uiObject.FindProperty("lockCanvas").objectReferenceValue = lockCanvas;
            uiObject.FindProperty("uiCamera").objectReferenceValue = uiCamera;
            uiObject.ApplyModifiedProperties();

            SerializedObject poolObject = new SerializedObject(poolManager);
            poolObject.FindProperty("rootPoolParent").objectReferenceValue = poolRoot;
            poolObject.ApplyModifiedProperties();

            if (rootGo.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(rootGo.scene);
            }

            EditorUtility.SetDirty(scope);
            Debug.Log("[HP Framework/VContainer] Bootstrap hierarchy and references are configured.", scope);
        }

        public static void AutoSetupScriptableObjects(RootLifetimeScope scope)
        {
            AudioLibrarySO audioLibrary = FindOrCreateAsset<AudioLibrarySO>(
                HPFrameworkProjectPaths.AudioLibraryPath);
            UICatalogSO uiCatalog = FindOrCreateAsset<UICatalogSO>(
                HPFrameworkProjectPaths.UICatalogPath);

            SerializedObject scopeObject = new SerializedObject(scope);
            SerializedProperty scopeAudioLibrary = scopeObject.FindProperty("audioLibrary");
            SerializedProperty scopeUiCatalog = scopeObject.FindProperty("uiCatalog");
            if (audioLibrary != null && scopeAudioLibrary.objectReferenceValue == null)
            {
                scopeAudioLibrary.objectReferenceValue = audioLibrary;
            }
            if (uiCatalog != null && scopeUiCatalog.objectReferenceValue == null)
            {
                scopeUiCatalog.objectReferenceValue = uiCatalog;
            }
            scopeObject.ApplyModifiedProperties();

            AudioManager audioManager = scope.GetComponent<AudioManager>();
            if (audioManager != null && audioLibrary != null)
            {
                SerializedObject audioObject = new SerializedObject(audioManager);
                SerializedProperty libraryProperty = audioObject.FindProperty("audioLibrary");
                if (libraryProperty.objectReferenceValue == null)
                {
                    libraryProperty.objectReferenceValue = audioLibrary;
                    audioObject.ApplyModifiedProperties();
                }
            }

            UIManager uiManager = scope.GetComponent<UIManager>();
            if (uiManager != null && uiCatalog != null)
            {
                SerializedObject uiObject = new SerializedObject(uiManager);
                SerializedProperty catalogProperty = uiObject.FindProperty("uiCatalog");
                if (catalogProperty.objectReferenceValue == null)
                {
                    catalogProperty.objectReferenceValue = uiCatalog;
                    uiObject.ApplyModifiedProperties();
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[HP Framework/VContainer] Project AudioLibrarySO and UICatalogSO are linked.", scope);
        }

        public static void AutoSetupDefaultRuntimeAssets(RootLifetimeScope scope)
        {
            MonoBehaviour inputManager = GetOptionalMonoBehaviour(
                scope.gameObject,
                InputManagerAssemblyQualifiedName);
            if (inputManager != null)
            {
                UnityEngine.Object defaultInput = AssetDatabase.LoadMainAssetAtPath(
                    HPFrameworkProjectPaths.DefaultInputActionsPath);
                if (defaultInput != null)
                {
                    SerializedObject inputObject = new SerializedObject(inputManager);
                    SerializedProperty inputActions = inputObject.FindProperty("inputActions");
                    if (inputActions != null && inputActions.objectReferenceValue == null)
                    {
                        inputActions.objectReferenceValue = defaultInput;
                        SerializedProperty defaultActionMap = inputObject.FindProperty("defaultActionMap");
                        if (defaultActionMap != null
                            && string.IsNullOrWhiteSpace(defaultActionMap.stringValue))
                        {
                            defaultActionMap.stringValue = "UI";
                        }
                        inputObject.ApplyModifiedProperties();
                    }
                }
            }

            UIManager uiManager = scope.GetComponent<UIManager>();
            if (uiManager != null)
            {
                GameObject toastPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    HPFrameworkProjectPaths.ToastPrefabPath);
                ToastUI toast = toastPrefab != null
                    ? toastPrefab.GetComponent<ToastUI>()
                    : null;
                if (toast != null)
                {
                    SerializedObject uiObject = new SerializedObject(uiManager);
                    SerializedProperty toastProperty = uiObject.FindProperty("toastPrefab");
                    if (toastProperty.objectReferenceValue == null)
                    {
                        toastProperty.objectReferenceValue = toast;
                        uiObject.ApplyModifiedProperties();
                    }
                }
            }
        }

        public static void AutoSetupBuildSettings()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            var scenes = EditorBuildSettings.scenes.ToList();

            bool added = false;
            if (!string.IsNullOrEmpty(activeScene.path)
                && !scenes.Any(scene => scene.path == activeScene.path))
            {
                scenes.Add(new EditorBuildSettingsScene(activeScene.path, true));
                added = true;
            }

            string loadingScenePath = AssetDatabase.FindAssets("t:Scene LoadingScene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(loadingScenePath)
                && !scenes.Any(scene => scene.path == loadingScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(loadingScenePath, true));
                added = true;
            }

            if (added)
            {
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log("[HP Framework/VContainer] Build Settings updated.");
            }
            else
            {
                Debug.Log("[HP Framework/VContainer] Build Settings already contain the required scenes.");
            }
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            if (!gameObject.TryGetComponent(out T component))
            {
                component = Undo.AddComponent<T>(gameObject);
            }
            return component;
        }

        private static MonoBehaviour GetOrAddOptionalMonoBehaviour(
            GameObject gameObject,
            string assemblyQualifiedTypeName)
        {
            Type type = Type.GetType(assemblyQualifiedTypeName, throwOnError: false);
            if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                return null;
            }

            return gameObject.GetComponent(type) as MonoBehaviour
                ?? Undo.AddComponent(gameObject, type) as MonoBehaviour;
        }

        private static MonoBehaviour GetOptionalMonoBehaviour(
            GameObject gameObject,
            string assemblyQualifiedTypeName)
        {
            Type type = Type.GetType(assemblyQualifiedTypeName, throwOnError: false);
            return type != null && typeof(MonoBehaviour).IsAssignableFrom(type)
                ? gameObject.GetComponent(type) as MonoBehaviour
                : null;
        }

        private static Transform FindOrCreateChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(childObject, "Create Bootstrap Child");
            child = childObject.transform;
            child.SetParent(parent, false);
            return child;
        }

        private static RectTransform FindOrCreateChildRect(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child == null)
            {
                GameObject childObject = new GameObject(childName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(childObject, "Create Bootstrap UI Child");
                child = childObject.transform;
                child.SetParent(parent, false);
            }

            return child as RectTransform;
        }

        private static void SetFullStretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
        }

        private static T FindOrCreateAsset<T>(string defaultPath) where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length > 0)
            {
                string existingPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<T>(existingPath);
            }

            string directory = Path.GetDirectoryName(defaultPath)?.Replace('\\', '/');
            EnsureAssetFolder(directory);

            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, defaultPath);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static void EnsureAssetFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string[] segments = path.Split('/');
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



