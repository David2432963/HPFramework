using UnityEngine;
using VContainer;
using VContainer.Unity;
using Sirenix.OdinInspector;
using Base.Audio;
using Base.UI;
using Base.Pooling;
using Base.Persistence;
using Base.Common;
using Base.IAP;
using Base.Assets;
using Base;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using System.IO;
using System.Linq;
#endif

namespace Base.Bootstrap
{
    /// <summary>
    /// Root composition root for the application.
    /// Manages 100% VContainer registration of shared services, interfaces, ScriptableObjects, IAP, AssetProvider (UniTask/Addressables), and global exception handling.
    /// Uses Odin Inspector for rich editor UI and 1-click setup buttons.
    /// </summary>
    public class RootLifetimeScope : LifetimeScope
    {
        [Title("⚡ Base Bootstrap & VContainer Scope", "Root Composition Root for All Game Services", TitleAlignments.Centered)]

        [BoxGroup("MonoBehaviour Managers")]
        [SerializeField, Required("Assign or click Auto Setup button below")] private AudioManager audioManager;
        [BoxGroup("MonoBehaviour Managers")]
        [SerializeField, Required("Assign or click Auto Setup button below")] private UIManager uiManager;
        [BoxGroup("MonoBehaviour Managers")]
        [SerializeField, Required("Assign or click Auto Setup button below")] private InputManager inputManager;
        [BoxGroup("MonoBehaviour Managers")]
        [SerializeField, Required("Assign or click Auto Setup button below")] private GameSceneManager gameSceneManager;
        [BoxGroup("MonoBehaviour Managers")]
        [SerializeField, Required("Assign or click Auto Setup button below")] private PoolManager poolManager;
        [BoxGroup("MonoBehaviour Managers")]
        [SerializeField, Required("Assign or click Auto Setup button below")] private HapticManager hapticManager;
        [BoxGroup("MonoBehaviour Managers")]
        [SerializeField, Required("Assign or click Auto Setup button below")] private Base.Core.MonoManager monoManager;

        [BoxGroup("Shared ScriptableObjects (Optional)")]
        [SerializeField, InlineEditor] private AudioLibrarySO audioLibrary;
        [BoxGroup("Shared ScriptableObjects (Optional)")]
        [SerializeField, InlineEditor] private UICatalogSO uiCatalog;

#if UNITY_EDITOR
        [Title("⚡ 1-Click Setup Utilities")]
        [Button("⚡ Auto Setup Hierarchy & References", ButtonSizes.Giant), GUIColor(0.3f, 0.7f, 1f)]
        private void AutoSetupHierarchyButton()
        {
            AutoSetupHierarchy();
        }

        [Button("📦 Auto Find / Create ScriptableObjects", ButtonSizes.Large), GUIColor(0.4f, 0.9f, 0.5f)]
        private void AutoSetupScriptableObjectsButton()
        {
            AutoSetupScriptableObjects();
        }

        [Button("🎬 Add Scenes to Build Settings", ButtonSizes.Large), GUIColor(1f, 0.7f, 0.3f)]
        private void AutoSetupBuildSettingsButton()
        {
            AutoSetupBuildSettings();
        }

        public void AutoSetupHierarchy()
        {
            GameObject rootGo = gameObject;
            Undo.RegisterFullObjectHierarchyUndo(rootGo, "Auto Setup Bootstrap Hierarchy");

            audioManager = GetOrAddComponent<AudioManager>(rootGo);
            uiManager = GetOrAddComponent<UIManager>(rootGo);
            inputManager = GetOrAddComponent<InputManager>(rootGo);
            gameSceneManager = GetOrAddComponent<GameSceneManager>(rootGo);
            poolManager = GetOrAddComponent<PoolManager>(rootGo);
            hapticManager = GetOrAddComponent<HapticManager>(rootGo);
            monoManager = GetOrAddComponent<Base.Core.MonoManager>(rootGo);

            Transform cameraTrans = FindOrCreateChild(transform, "UICamera");
            Camera uiCamera = GetOrAddComponent<Camera>(cameraTrans.gameObject);
            uiCamera.orthographic = true;
            uiCamera.orthographicSize = 5f;
            uiCamera.nearClipPlane = 0.3f;
            uiCamera.farClipPlane = 1000f;
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.backgroundColor = new Color(0, 0, 0, 0);
            uiCamera.cullingMask = 1 << 5;

            Transform canvasTrans = FindOrCreateChild(transform, "UICanvas");
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

            Transform poolRoot = FindOrCreateChild(transform, "PoolRoot");

            SerializedObject uiSO = new SerializedObject(uiManager);
            uiSO.FindProperty("screenCanvas").objectReferenceValue = screenCanvas;
            uiSO.FindProperty("popupCanvas").objectReferenceValue = popupCanvas;
            uiSO.FindProperty("notiCanvas").objectReferenceValue = notiCanvas;
            uiSO.FindProperty("lockCanvas").objectReferenceValue = lockCanvas;
            uiSO.FindProperty("uiCamera").objectReferenceValue = uiCamera;
            uiSO.ApplyModifiedProperties();

            SerializedObject poolSO = new SerializedObject(poolManager);
            poolSO.FindProperty("rootPoolParent").objectReferenceValue = poolRoot;
            poolSO.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(rootGo.scene);
            Debug.Log("⚡ [Bootstrap Setup] Successfully setup Bootstrap hierarchy, Managers, Camera & Canvases!");
        }

        public void AutoSetupScriptableObjects()
        {
            var audioLib = FindOrCreateAsset<AudioLibrarySO>("Assets/_Base/Data/DefaultAudioLibrary.asset");
            var uiCat = FindOrCreateAsset<UICatalogSO>("Assets/_Base/Data/DefaultUICatalog.asset");

            if (audioLib != null) audioLibrary = audioLib;
            if (uiCat != null) uiCatalog = uiCat;

            if (audioManager != null && audioLib != null)
            {
                SerializedObject audioSO = new SerializedObject(audioManager);
                audioSO.FindProperty("audioLibrary").objectReferenceValue = audioLib;
                audioSO.ApplyModifiedProperties();
            }

            if (uiManager != null && uiCat != null)
            {
                SerializedObject uiSO = new SerializedObject(uiManager);
                uiSO.FindProperty("uiCatalog").objectReferenceValue = uiCat;
                uiSO.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(this);
            Debug.Log("📦 [Bootstrap Setup] Successfully linked AudioLibrarySO and UICatalogSO!");
        }

        public void AutoSetupBuildSettings()
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
                Debug.Log("🎬 [Bootstrap Setup] Successfully updated Build Settings with current scene & LoadingScene!");
            }
            else
            {
                Debug.Log("🎬 [Bootstrap Setup] All required scenes are already in Build Settings.");
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
#endif

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPointExceptionHandler(ex =>
            {
                BaseLog.LogError($"[VContainer Exception] {ex.Message}\n{ex.StackTrace}");
            });

            if (audioLibrary != null)
            {
                builder.RegisterInstance(audioLibrary);
            }

            if (uiCatalog != null)
            {
                builder.RegisterInstance(uiCatalog);
            }

            // Register Asset Provider (UniTask & Addressables Integration)
            builder.Register<AddressablesAssetProvider>(Lifetime.Singleton)
                   .As<IAssetProvider>();

            builder.Register<SettingsManager>(Lifetime.Singleton)
                   .AsSelf()
                   .As<ISettingsProvider>()
                   .As<IInitializable>()
                   .As<System.IDisposable>();

            builder.Register<ProcedureManager>(Lifetime.Singleton)
                   .AsSelf();

            builder.Register<SimulatedIAPProvider>(Lifetime.Singleton)
                   .As<IIAPProvider>();
            builder.Register<IAPService>(Lifetime.Singleton);

            if (audioManager != null)
            {
                builder.RegisterComponent(audioManager)
                       .AsSelf()
                       .As<IAudioService>();
            }

            if (uiManager != null)
            {
                builder.RegisterComponent(uiManager)
                       .AsSelf()
                       .As<IUIService>();
            }

            if (inputManager != null)
            {
                builder.RegisterComponent(inputManager);
            }

            if (gameSceneManager != null)
            {
                builder.RegisterComponent(gameSceneManager)
                       .AsSelf()
                       .As<IProcedureSceneLoader>();
            }

            if (poolManager != null)
            {
                builder.RegisterComponent(poolManager)
                       .AsSelf()
                       .As<IPoolService>();
            }

            if (hapticManager != null)
            {
                builder.RegisterComponent(hapticManager)
                       .AsSelf()
                       .As<IHapticService>();
            }

            if (monoManager != null)
            {
                builder.RegisterComponent(monoManager)
                       .AsSelf();
            }
        }
    }
}
