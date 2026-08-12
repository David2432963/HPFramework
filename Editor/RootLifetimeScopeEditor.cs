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
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HP.Framework.Editor
{
    [CustomEditor(typeof(RootLifetimeScope), true)]
    public class RootLifetimeScopeEditor : UnityEditor.Editor
    {
        private const string InputManagerAssemblyQualifiedName =
            "HP.Framework.Input.InputManager, HP.Framework.Input";
        private const string InputSystemUIInputModuleAssemblyQualifiedName =
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem";
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
                    "This migrates framework-owned managers into the canonical domain hierarchy " +
                    "and reapplies HP Framework camera, canvas and layout defaults. Continue?",
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
            if (GUILayout.Button("Configure Manual Bootstrap Mode", GUILayout.Height(26f)))
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
            Transform bootstrapRoot = rootGo.transform;
            Undo.RegisterFullObjectHierarchyUndo(rootGo, "Auto Setup Bootstrap Hierarchy");

            SerializedObject scopeObject = new SerializedObject(scope);
            AudioManager audioManager = ResolveOrCreateManagedComponent<AudioManager>(
                scopeObject.FindProperty("audioManager"),
                bootstrapRoot,
                "Audio",
                resetDefaults);
            UIManager uiManager = ResolveOrCreateManagedComponent<UIManager>(
                scopeObject.FindProperty("uiManager"),
                bootstrapRoot,
                "UI",
                resetDefaults);
            MonoBehaviour inputManager = ResolveOrCreateOptionalManagedComponent(
                scopeObject.FindProperty("inputManager"),
                bootstrapRoot,
                "Input",
                InputManagerAssemblyQualifiedName,
                resetDefaults);
            GameSceneManager sceneManager = ResolveOrCreateManagedComponent<GameSceneManager>(
                scopeObject.FindProperty("gameSceneManager"),
                bootstrapRoot,
                "Scene",
                resetDefaults);
            PoolManager poolManager = ResolveOrCreateManagedComponent<PoolManager>(
                scopeObject.FindProperty("poolManager"),
                bootstrapRoot,
                "Pools",
                resetDefaults);
            HapticManager hapticManager = ResolveOrCreateManagedComponent<HapticManager>(
                scopeObject.FindProperty("hapticManager"),
                bootstrapRoot,
                "Haptics",
                resetDefaults);

            Transform uiOwner = uiManager != null ? uiManager.transform : bootstrapRoot;
            bool cameraCreated = uiOwner.Find("UICamera") == null
                && bootstrapRoot.Find("UICamera") == null;
            Transform cameraTransform = FindOrCreateOwnedChild(
                uiOwner,
                bootstrapRoot,
                "UICamera",
                resetDefaults);
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
                uiCamera.allowHDR = false;
                uiCamera.allowMSAA = false;
                uiCamera.allowDynamicResolution = false;
                uiCamera.useOcclusionCulling = false;
            }

            URPCameraStackUtility.ConfigureAsOverlay(uiCamera);

            bool canvasCreated = uiOwner.Find("UICanvas") == null
                && bootstrapRoot.Find("UICanvas") == null;
            Transform canvasTransform = FindOrCreateOwnedChild(
                uiOwner,
                bootstrapRoot,
                "UICanvas",
                resetDefaults);
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
                // Start lean. TextMeshProUGUI enables the extra channels it requires when text
                // is actually present on this canvas.
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
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

            RectTransform screenRoot = FindOrCreateRenamedChildRect(
                canvasTransform,
                "ScreenRoot",
                resetDefaults,
                out bool screenCreated,
                "ScreenCanvas");
            RectTransform popupRoot = FindOrCreateRenamedChildRect(
                canvasTransform,
                "PopupRoot",
                resetDefaults,
                out bool popupCreated,
                "PopupCanvas");
            RectTransform notificationRoot = FindOrCreateRenamedChildRect(
                canvasTransform,
                "NotificationRoot",
                resetDefaults,
                out bool notificationCreated,
                "NotiCanvas",
                "NotificationCanvas");
            RectTransform inputBlocker = FindOrCreateRenamedChildRect(
                canvasTransform,
                "InputBlocker",
                resetDefaults,
                out bool blockerCreated,
                "LockCanvas");
            if (resetDefaults || screenCreated) SetFullStretch(screenRoot);
            if (resetDefaults || popupCreated) SetFullStretch(popupRoot);
            if (resetDefaults || notificationCreated) SetFullStretch(notificationRoot);
            if (resetDefaults || blockerCreated) SetFullStretch(inputBlocker);

            GetOrAddComponent<CanvasGroup>(inputBlocker.gameObject);
            bool blockerImageCreated = !inputBlocker.TryGetComponent(out Image blockerImage);
            blockerImage ??= GetOrAddComponent<Image>(inputBlocker.gameObject);
            if (resetDefaults || blockerCreated || blockerImageCreated)
            {
                blockerImage.color = new Color(0f, 0f, 0f, 0f);
                blockerImage.raycastTarget = true;
                inputBlocker.gameObject.SetActive(false);
            }

            ConfigureEventSystem(uiOwner, bootstrapRoot, resetDefaults);

            SerializedObject poolObject = new SerializedObject(poolManager);
            SerializedProperty poolRootProperty = poolObject.FindProperty("rootPoolParent");
            Transform poolRoot = ResolvePoolRoot(
                poolManager,
                bootstrapRoot,
                poolRootProperty.objectReferenceValue as Transform,
                resetDefaults);
            poolRootProperty.objectReferenceValue = poolRoot;
            poolObject.ApplyModifiedProperties();

            scopeObject.Update();
            scopeObject.FindProperty("audioManager").objectReferenceValue = audioManager;
            scopeObject.FindProperty("uiManager").objectReferenceValue = uiManager;
            scopeObject.FindProperty("inputManager").objectReferenceValue = inputManager;
            scopeObject.FindProperty("gameSceneManager").objectReferenceValue = sceneManager;
            scopeObject.FindProperty("poolManager").objectReferenceValue = poolManager;
            scopeObject.FindProperty("hapticManager").objectReferenceValue = hapticManager;
            scopeObject.ApplyModifiedProperties();

            SerializedObject uiObject = new SerializedObject(uiManager);
            uiObject.FindProperty("screenRoot").objectReferenceValue = screenRoot;
            uiObject.FindProperty("popupRoot").objectReferenceValue = popupRoot;
            uiObject.FindProperty("notificationRoot").objectReferenceValue = notificationRoot;
            uiObject.FindProperty("inputBlocker").objectReferenceValue = inputBlocker;
            uiObject.FindProperty("uiCamera").objectReferenceValue = uiCamera;
            uiObject.ApplyModifiedProperties();

            if (rootGo.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(rootGo.scene);
            }

            EditorUtility.SetDirty(scope);
            Debug.Log("[HP Framework/VContainer] Bootstrap hierarchy and references are configured.", scope);
        }

        public static void AutoSetupScriptableObjects(RootLifetimeScope scope)
        {
            SerializedObject scopeObject = new SerializedObject(scope);
            SerializedProperty scopeAudioLibrary = scopeObject.FindProperty("audioLibrary");
            SerializedProperty scopeUiCatalog = scopeObject.FindProperty("uiCatalog");

            AudioLibrarySO audioLibrary = FindOrCreateAsset(
                scopeAudioLibrary.objectReferenceValue as AudioLibrarySO,
                HPFrameworkProjectPaths.AudioLibraryPath);
            UICatalogSO uiCatalog = FindOrCreateAsset(
                scopeUiCatalog.objectReferenceValue as UICatalogSO,
                HPFrameworkProjectPaths.UICatalogPath);
            if (audioLibrary != null && scopeAudioLibrary.objectReferenceValue == null)
            {
                scopeAudioLibrary.objectReferenceValue = audioLibrary;
            }
            if (uiCatalog != null && scopeUiCatalog.objectReferenceValue == null)
            {
                scopeUiCatalog.objectReferenceValue = uiCatalog;
            }
            scopeObject.ApplyModifiedProperties();

            AudioManager audioManager = GetReferencedComponent<AudioManager>(
                scope,
                "audioManager");
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

            UIManager uiManager = GetReferencedComponent<UIManager>(
                scope,
                "uiManager");
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
            MonoBehaviour inputManager = GetReferencedOptionalMonoBehaviour(
                scope,
                "inputManager",
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

            UIManager uiManager = GetReferencedComponent<UIManager>(
                scope,
                "uiManager");
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

        private static T ResolveOrCreateManagedComponent<T>(
            SerializedProperty referenceProperty,
            Transform bootstrapRoot,
            string ownerName,
            bool resetDefaults)
            where T : Component
        {
            T referenced = referenceProperty.objectReferenceValue as T;
            if (!resetDefaults)
            {
                if (referenced != null)
                {
                    return referenced;
                }

                T existing = bootstrapRoot.GetComponentInChildren<T>(true);
                if (existing != null)
                {
                    return existing;
                }
            }

            T source = referenced ?? bootstrapRoot.GetComponentInChildren<T>(true);
            Transform owner = FindOrCreateChild(bootstrapRoot, ownerName);
            T canonical = GetOrAddComponent<T>(owner.gameObject);
            if (resetDefaults && source != null && source != canonical)
            {
                EditorUtility.CopySerialized(source, canonical);
                Undo.DestroyObjectImmediate(source);
            }

            return canonical;
        }

        private static MonoBehaviour ResolveOrCreateOptionalManagedComponent(
            SerializedProperty referenceProperty,
            Transform bootstrapRoot,
            string ownerName,
            string assemblyQualifiedTypeName,
            bool resetDefaults)
        {
            Type type = Type.GetType(assemblyQualifiedTypeName, throwOnError: false);
            if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                return null;
            }

            MonoBehaviour referenced = referenceProperty.objectReferenceValue as MonoBehaviour;
            if (referenced != null && !type.IsInstanceOfType(referenced))
            {
                referenced = null;
            }

            if (!resetDefaults)
            {
                if (referenced != null)
                {
                    return referenced;
                }

                MonoBehaviour existing = bootstrapRoot.GetComponentInChildren(type, true) as MonoBehaviour;
                if (existing != null)
                {
                    return existing;
                }
            }

            MonoBehaviour source = referenced
                ?? bootstrapRoot.GetComponentInChildren(type, true) as MonoBehaviour;
            Transform owner = FindOrCreateChild(bootstrapRoot, ownerName);
            MonoBehaviour canonical = owner.GetComponent(type) as MonoBehaviour
                ?? Undo.AddComponent(owner.gameObject, type) as MonoBehaviour;
            if (resetDefaults && source != null && source != canonical)
            {
                EditorUtility.CopySerialized(source, canonical);
                Undo.DestroyObjectImmediate(source);
            }

            return canonical;
        }

        private static T GetReferencedComponent<T>(RootLifetimeScope scope, string propertyName)
            where T : Component
        {
            SerializedObject scopeObject = new SerializedObject(scope);
            T referenced = scopeObject.FindProperty(propertyName)?.objectReferenceValue as T;
            return referenced != null
                ? referenced
                : scope.GetComponentInChildren<T>(true);
        }

        private static MonoBehaviour GetReferencedOptionalMonoBehaviour(
            RootLifetimeScope scope,
            string propertyName,
            string assemblyQualifiedTypeName)
        {
            Type type = Type.GetType(assemblyQualifiedTypeName, throwOnError: false);
            if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                return null;
            }

            SerializedObject scopeObject = new SerializedObject(scope);
            MonoBehaviour referenced =
                scopeObject.FindProperty(propertyName)?.objectReferenceValue as MonoBehaviour;
            if (referenced != null && type.IsInstanceOfType(referenced))
            {
                return referenced;
            }

            return scope.GetComponentInChildren(type, true) as MonoBehaviour;
        }

        private static Transform FindOrCreateOwnedChild(
            Transform owner,
            Transform legacyParent,
            string childName,
            bool migrateToOwner)
        {
            Transform child = owner.Find(childName);
            if (child != null)
            {
                return child;
            }

            if (owner != legacyParent)
            {
                child = legacyParent.Find(childName);
                if (child != null)
                {
                    if (migrateToOwner)
                    {
                        Undo.SetTransformParent(child, owner, $"Move {childName} To {owner.name}");
                    }
                    return child;
                }
            }

            return FindOrCreateChild(owner, childName);
        }

        private static RectTransform FindOrCreateRenamedChildRect(
            Transform parent,
            string canonicalName,
            bool renameLegacy,
            out bool created,
            params string[] legacyNames)
        {
            Transform child = parent.Find(canonicalName);
            if (child != null)
            {
                RectTransform existingRect = child as RectTransform;
                if (existingRect != null)
                {
                    created = false;
                    return existingRect;
                }

                Debug.LogWarning(
                    $"[HP Framework/VContainer] '{canonicalName}' exists but is not a RectTransform. " +
                    "A canonical UI root will be created instead.",
                    child);
            }

            for (int i = 0; i < legacyNames.Length; i++)
            {
                child = parent.Find(legacyNames[i]);
                if (child == null)
                {
                    continue;
                }

                RectTransform legacyRect = child as RectTransform;
                if (legacyRect == null)
                {
                    Debug.LogWarning(
                        $"[HP Framework/VContainer] Legacy UI root '{legacyNames[i]}' is not a RectTransform " +
                        "and cannot be migrated automatically.",
                        child);
                    continue;
                }

                created = false;
                if (renameLegacy)
                {
                    Undo.RecordObject(child.gameObject, $"Rename {legacyNames[i]} To {canonicalName}");
                    child.gameObject.name = canonicalName;
                }
                return legacyRect;
            }

            created = true;
            return FindOrCreateUniqueChildRect(parent, canonicalName);
        }

        private static void ConfigureEventSystem(
            Transform uiOwner,
            Transform bootstrapRoot,
            bool resetDefaults)
        {
            Transform eventSystemTransform = FindOrCreateOwnedChild(
                uiOwner,
                bootstrapRoot,
                "EventSystem",
                resetDefaults);
            EventSystem eventSystem = GetOrAddComponent<EventSystem>(eventSystemTransform.gameObject);
            BaseInputModule[] existingModules = eventSystem.GetComponents<BaseInputModule>();
            if (!resetDefaults && existingModules.Length > 0)
            {
                return;
            }

            Type inputSystemModuleType = Type.GetType(
                InputSystemUIInputModuleAssemblyQualifiedName,
                throwOnError: false);
            Type preferredModuleType = inputSystemModuleType != null
                && typeof(BaseInputModule).IsAssignableFrom(inputSystemModuleType)
                ? inputSystemModuleType
                : typeof(StandaloneInputModule);

            if (resetDefaults)
            {
                for (int i = 0; i < existingModules.Length; i++)
                {
                    BaseInputModule module = existingModules[i];
                    if (module != null && module.GetType() != preferredModuleType)
                    {
                        Undo.DestroyObjectImmediate(module);
                    }
                }
            }

            if (eventSystem.GetComponent(preferredModuleType) == null)
            {
                Undo.AddComponent(eventSystem.gameObject, preferredModuleType);
            }
        }

        private static Transform ResolvePoolRoot(
            PoolManager poolManager,
            Transform bootstrapRoot,
            Transform currentPoolRoot,
            bool resetDefaults)
        {
            if (!resetDefaults && currentPoolRoot != null)
            {
                return currentPoolRoot;
            }

            if (!resetDefaults)
            {
                Transform legacyRoot = bootstrapRoot.Find("PoolRoot");
                return legacyRoot != null ? legacyRoot : poolManager.transform;
            }

            Transform oldPoolRoot = bootstrapRoot.Find("PoolRoot");
            if (oldPoolRoot != null && oldPoolRoot != poolManager.transform)
            {
                while (oldPoolRoot.childCount > 0)
                {
                    Undo.SetTransformParent(
                        oldPoolRoot.GetChild(0),
                        poolManager.transform,
                        "Migrate Pool Root Child");
                }
                Undo.DestroyObjectImmediate(oldPoolRoot.gameObject);
            }

            return poolManager.transform;
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
                return FindOrCreateUniqueChildRect(parent, childName);
            }

            RectTransform rect = child as RectTransform;
            if (rect != null)
            {
                return rect;
            }

            return FindOrCreateUniqueChildRect(parent, childName);
        }

        private static RectTransform FindOrCreateUniqueChildRect(Transform parent, string childName)
        {
            string resolvedName = childName;
            int suffix = 1;
            while (parent.Find(resolvedName) != null)
            {
                resolvedName = $"{childName}_{suffix++}";
            }

            GameObject childObject = new GameObject(resolvedName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(childObject, "Create Bootstrap UI Child");
            RectTransform rect = childObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void SetFullStretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
        }

        private static T FindOrCreateAsset<T>(T currentAsset, string defaultPath)
            where T : ScriptableObject
        {
            if (currentAsset != null)
            {
                return currentAsset;
            }

            T canonicalAsset = AssetDatabase.LoadAssetAtPath<T>(defaultPath);
            if (canonicalAsset != null)
            {
                return canonicalAsset;
            }

            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length == 1)
            {
                string onlyPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                T onlyAsset = AssetDatabase.LoadAssetAtPath<T>(onlyPath);
                if (onlyAsset != null)
                {
                    return onlyAsset;
                }
            }
            else if (guids.Length > 1)
            {
                Debug.LogWarning(
                    $"[HP Framework/VContainer] Found multiple {typeof(T).Name} assets. " +
                    $"Repair will create/use the deterministic framework asset at '{defaultPath}' " +
                    "instead of selecting an arbitrary project asset.");
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



