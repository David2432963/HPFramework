#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Base.Bootstrap;
using Base.UI;

namespace Base.Editor
{
    [CustomEditor(typeof(RootLifetimeScope))]
    public class RootLifetimeScopeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            RootLifetimeScope scope = (RootLifetimeScope)target;

            // Title Banner
            EditorGUILayout.Space(5);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 0.8f, 0.4f) }
            };
            EditorGUILayout.LabelField("âš¡ Base Bootstrap Setup Utility", headerStyle);
            EditorGUILayout.HelpBox("KÃ©o tháº£ Prefab Bootstrap vÃ o scene rá»“i báº¥m nÃºt bÃªn dÆ°á»›i Ä‘á»ƒ tá»± Ä‘á»™ng táº¡o & káº¿t ná»‘i Hierarchy, Canvases, Camera vÃ  ScriptableObjects trong 1-click.", MessageType.Info);
            EditorGUILayout.Space(5);

            // Action Buttons Section
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1.0f);
            if (GUILayout.Button("âš¡ Auto Setup Hierarchy & References", GUILayout.Height(35)))
            {
                AutoSetupHierarchy(scope);
            }

            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);
            if (GUILayout.Button("ðŸ“¦ Auto Find / Create ScriptableObjects (Audio & UI)", GUILayout.Height(30)))
            {
                AutoSetupScriptableObjects(scope);
            }

            GUI.backgroundColor = new Color(1.0f, 0.7f, 0.3f);
            if (GUILayout.Button("ðŸŽ¬ Add Current & Loading Scene to Build Settings", GUILayout.Height(30)))
            {
                AutoSetupBuildSettings();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Standard Inspector Properties
            DrawDefaultInspector();
        }

        public static void AutoSetupHierarchy(RootLifetimeScope scope)
        {
            GameObject rootGo = scope.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(rootGo, "Auto Setup Bootstrap Hierarchy");

            // 1. Ensure Manager Components exist on root
            var audioMgr = GetOrAddComponent<AudioManager>(rootGo);
            var uiMgr = GetOrAddComponent<UIManager>(rootGo);
            var inputMgr = GetOrAddComponent<InputManager>(rootGo);
            var gameSceneMgr = GetOrAddComponent<GameSceneManager>(rootGo);
            var poolMgr = GetOrAddComponent<PoolManager>(rootGo);
            var hapticMgr = GetOrAddComponent<HapticManager>(rootGo);

            // 2. Setup Children Hierarchy (Camera, Canvases, PoolRoot)
            Transform cameraTrans = FindOrCreateChild(rootGo.transform, "UICamera");
            Camera uiCamera = GetOrAddComponent<Camera>(cameraTrans.gameObject);
            uiCamera.orthographic = true;
            uiCamera.orthographicSize = 5f;
            uiCamera.nearClipPlane = 0.3f;
            uiCamera.farClipPlane = 1000f;
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.backgroundColor = new Color(0, 0, 0, 0);
            uiCamera.cullingMask = 1 << 5; // UI layer

            Transform canvasTrans = FindOrCreateChild(rootGo.transform, "UICanvas");
            Canvas canvas = GetOrAddComponent<Canvas>(canvasTrans.gameObject);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 100f;

            CanvasScaler scaler = GetOrAddComponent<CanvasScaler>(canvasTrans.gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GetOrAddComponent<GraphicRaycaster>(canvasTrans.gameObject);

            // Child RectTransforms inside UICanvas
            RectTransform screenCanvas = FindOrCreateChildRect(canvasTrans, "ScreenCanvas");
            SetFullStretch(screenCanvas);

            RectTransform popupCanvas = FindOrCreateChildRect(canvasTrans, "PopupCanvas");
            SetFullStretch(popupCanvas);

            RectTransform notiCanvas = FindOrCreateChildRect(canvasTrans, "NotiCanvas");
            SetFullStretch(notiCanvas);

            RectTransform lockCanvas = FindOrCreateChildRect(canvasTrans, "LockCanvas");
            SetFullStretch(lockCanvas);
            GetOrAddComponent<CanvasGroup>(lockCanvas.gameObject);
            var lockImage = GetOrAddComponent<Image>(lockCanvas.gameObject);
            lockImage.color = new Color(0, 0, 0, 0);
            lockCanvas.gameObject.SetActive(false);

            Transform poolRoot = FindOrCreateChild(rootGo.transform, "PoolRoot");

            // 3. SerializedObject Assign References
            SerializedObject scopeSO = new SerializedObject(scope);
            scopeSO.FindProperty("audioManager").objectReferenceValue = audioMgr;
            scopeSO.FindProperty("uiManager").objectReferenceValue = uiMgr;
            scopeSO.FindProperty("inputManager").objectReferenceValue = inputMgr;
            scopeSO.FindProperty("gameSceneManager").objectReferenceValue = gameSceneMgr;
            scopeSO.FindProperty("poolManager").objectReferenceValue = poolMgr;
            scopeSO.FindProperty("hapticManager").objectReferenceValue = hapticMgr;
            scopeSO.ApplyModifiedProperties();

            SerializedObject uiSO = new SerializedObject(uiMgr);
            uiSO.FindProperty("screenCanvas").objectReferenceValue = screenCanvas;
            uiSO.FindProperty("popupCanvas").objectReferenceValue = popupCanvas;
            uiSO.FindProperty("notiCanvas").objectReferenceValue = notiCanvas;
            uiSO.FindProperty("lockCanvas").objectReferenceValue = lockCanvas;
            uiSO.FindProperty("uiCamera").objectReferenceValue = uiCamera;
            uiSO.ApplyModifiedProperties();

            SerializedObject poolSO = new SerializedObject(poolMgr);
            poolSO.FindProperty("rootPoolParent").objectReferenceValue = poolRoot;
            poolSO.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(rootGo.scene);
            Debug.Log("âš¡ [Bootstrap Setup] Successfully setup Bootstrap hierarchy, Managers, Camera & Canvases!");
        }

        public static void AutoSetupScriptableObjects(RootLifetimeScope scope)
        {
            var audioLib = FindOrCreateAsset<AudioLibrarySO>("Assets/_Base/Data/DefaultAudioLibrary.asset");
            var uiCat = FindOrCreateAsset<UICatalogSO>("Assets/_Base/Data/DefaultUICatalog.asset");

            SerializedObject scopeSO = new SerializedObject(scope);
            if (audioLib != null) scopeSO.FindProperty("audioLibrary").objectReferenceValue = audioLib;
            if (uiCat != null) scopeSO.FindProperty("uiCatalog").objectReferenceValue = uiCat;
            scopeSO.ApplyModifiedProperties();

            var audioMgr = scope.GetComponent<AudioManager>();
            if (audioMgr != null && audioLib != null)
            {
                SerializedObject audioSO = new SerializedObject(audioMgr);
                audioSO.FindProperty("audioLibrary").objectReferenceValue = audioLib;
                audioSO.ApplyModifiedProperties();
            }

            var uiMgr = scope.GetComponent<UIManager>();
            if (uiMgr != null && uiCat != null)
            {
                SerializedObject uiSO = new SerializedObject(uiMgr);
                uiSO.FindProperty("uiCatalog").objectReferenceValue = uiCat;
                uiSO.ApplyModifiedProperties();
            }

            Debug.Log("ðŸ“¦ [Bootstrap Setup] Successfully linked AudioLibrarySO and UICatalogSO!");
        }

        public static void AutoSetupBuildSettings()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            var scenes = EditorBuildSettings.scenes.ToList();

            bool added = false;
            if (!string.IsNullOrEmpty(activeScene.path) && !scenes.Any(s => s.path == activeScene.path))
            {
                scenes.Add(new EditorBuildSettingsScene(activeScene.path, true));
                added = true;
            }

            string loadingScenePath = AssetDatabase.FindAssets("t:Scene LoadingScene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(loadingScenePath) && !scenes.Any(s => s.path == loadingScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(loadingScenePath, true));
                added = true;
            }

            if (added)
            {
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log("ðŸŽ¬ [Bootstrap Setup] Successfully updated Build Settings with current scene & LoadingScene!");
            }
            else
            {
                Debug.Log("ðŸŽ¬ [Bootstrap Setup] All required scenes are already in Build Settings.");
            }
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            if (!go.TryGetComponent<T>(out var comp))
            {
                comp = go.AddComponent<T>();
            }
            return comp;
        }

        private static Transform FindOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                GameObject childGo = new GameObject(name);
                child = childGo.transform;
                child.SetParent(parent, false);
            }
            return child;
        }

        private static RectTransform FindOrCreateChildRect(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                GameObject childGo = new GameObject(name, typeof(RectTransform));
                child = childGo.transform;
                child.SetParent(parent, false);
            }
            return child as RectTransform;
        }

        private static void SetFullStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        private static T FindOrCreateAsset<T>(string defaultPath) where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<T>(path);
            }

            T asset = ScriptableObject.CreateInstance<T>();
            string dir = Path.GetDirectoryName(defaultPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            AssetDatabase.CreateAsset(asset, defaultPath);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
#endif
