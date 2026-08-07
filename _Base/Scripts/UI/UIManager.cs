using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Base.UI;
using Base.Pooling;
using Base;
using VContainer;
using VContainer.Unity;

/// <summary>
/// UI Manager for screens and popups.
/// Implements IUIService with VContainer native atomic instantiation (Instantiate + Inject in 1 step).
/// Uses only Unity runtime dependencies; URP camera stacking is attached through reflection when available.
/// </summary>
public class UIManager : MonoBehaviour, IUIService, IInitializable
{
    [Header("UI Canvases & Camera")]
    [SerializeField] private RectTransform screenCanvas;
    [SerializeField] private RectTransform popupCanvas;
    [SerializeField] private RectTransform notiCanvas;
    [SerializeField] private RectTransform lockCanvas;
    [SerializeField] private Camera uiCamera;

    [Header("UI Catalog & Prefabs")]
    [SerializeField] private UICatalogSO uiCatalog;
    [SerializeField] private ToastUI toastPrefab;

    private readonly Dictionary<Type, BasePopup> popups = new Dictionary<Type, BasePopup>();
    private readonly Dictionary<Type, BaseScreen> screens = new Dictionary<Type, BaseScreen>();
    private readonly Dictionary<Type, UICatalogSO.UIEntry> popupCatalogEntries = new Dictionary<Type, UICatalogSO.UIEntry>();
    private readonly Dictionary<Type, UICatalogSO.UIEntry> screenCatalogEntries = new Dictionary<Type, UICatalogSO.UIEntry>();
    private readonly List<BasePopup> activePopupsCache = new List<BasePopup>();

    private IPoolService poolService;
    private IObjectResolver objectResolver;
    private bool initialized;

    public event Action<BasePopup> PopupShown;
    public event Action<BasePopup> PopupHidden;

    public Camera UICamera => uiCamera;

    [Inject]
    public void Construct(IPoolService poolService, IObjectResolver objectResolver)
    {
        this.poolService = poolService;
        this.objectResolver = objectResolver;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachUICameraToMainCamera();
    }

    private void AttachUICameraToMainCamera()
    {
        if (uiCamera == null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Type cameraDataType = Type.GetType(
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (cameraDataType == null)
        {
            return;
        }

        Component cameraData = mainCamera.GetComponent(cameraDataType);
        System.Reflection.PropertyInfo stackProperty = cameraDataType.GetProperty("cameraStack");
        IList cameraStack = stackProperty?.GetValue(cameraData) as IList;
        if (cameraStack != null && !cameraStack.Contains(uiCamera))
        {
            cameraStack.Add(uiCamera);
        }
    }

    public void Initialize()
    {
        if (initialized)
        {
            return;
        }

        popups.Clear();
        screens.Clear();
        popupCatalogEntries.Clear();
        screenCatalogEntries.Clear();
        activePopupsCache.Clear();

        if (uiCatalog != null)
        {
            foreach (var entry in uiCatalog.GetPopupEntries())
            {
                if (entry != null && entry.TryGetRuntimeType(out var type))
                {
                    popupCatalogEntries[type] = entry;
                }
            }

            foreach (var entry in uiCatalog.GetScreenEntries())
            {
                if (entry != null && entry.TryGetRuntimeType(out var type))
                {
                    screenCatalogEntries[type] = entry;
                }
            }

        }

        initialized = true;
        AttachUICameraToMainCamera();
    }

    public void ConfigureCatalog(UICatalogSO catalog)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        uiCatalog = catalog;
        initialized = false;
        Initialize();
    }

    public bool HasVisiblePopup
    {
        get
        {
            foreach (var pair in popups)
            {
                var popup = pair.Value;
                if (popup != null && popup.gameObject.activeSelf)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public void RegisterPopup(BasePopup popup)
    {
        if (popup != null)
        {
            popups[popup.GetType()] = popup;
        }
    }

    public void UnregisterPopup(BasePopup popup)
    {
        if (popup != null)
        {
            popups.Remove(popup.GetType());
        }
    }

    public void RegisterScreen(BaseScreen screen)
    {
        if (screen != null)
        {
            screens[screen.GetType()] = screen;
        }
    }

    public void UnregisterScreen(BaseScreen screen)
    {
        if (screen != null)
        {
            screens.Remove(screen.GetType());
        }
    }

    public T OpenPopup<T>() where T : BasePopup
    {
        return OpenPopup(typeof(T)) as T;
    }

    public T GetOrCreatePopup<T>() where T : BasePopup
    {
        return TryGetOrCreatePopup(typeof(T), out BasePopup popup)
            ? popup as T
            : null;
    }

    public BasePopup OpenPopup(Type type)
    {
        BaseLog.Log($"[UIManager] OpenPopup: {type.Name}");
        if (TryGetOrCreatePopup(type, out var popup))
        {
            popup.Show();
            return popup;
        }
        return null;
    }

    public void ClosePopup<T>() where T : BasePopup
    {
        BaseLog.Log($"[UIManager] ClosePopup: {typeof(T).Name}");
        if (popups.TryGetValue(typeof(T), out var popup))
        {
            popup.Hide();
        }
    }

    public T GetPopup<T>() where T : BasePopup
    {
        return popups.TryGetValue(typeof(T), out var popup) ? popup as T : null;
    }

    public bool IsPopupRegistered<T>() where T : BasePopup
    {
        return popups.ContainsKey(typeof(T));
    }

    public void CloseAllPopups()
    {
        activePopupsCache.Clear();
        foreach (var pair in popups)
        {
            var popup = pair.Value;
            if (popup != null)
            {
                activePopupsCache.Add(popup);
            }
        }

        for (int i = 0; i < activePopupsCache.Count; i++)
        {
            var popup = activePopupsCache[i];
            if (popup != null && popup.gameObject.activeSelf)
            {
                popup.Hide();
            }
        }
        activePopupsCache.Clear();
    }

    public void ClearPopups()
    {
        popups.Clear();
    }

    public T ShowScreen<T>() where T : BaseScreen
    {
        BaseLog.Log($"[UIManager] ShowScreen: {typeof(T).Name}");
        if (TryGetOrCreateScreen(typeof(T), out var screen))
        {
            screen.Show();
            return screen as T;
        }
        return null;
    }

    public T GetOrCreateScreen<T>() where T : BaseScreen
    {
        if (TryGetOrCreateScreen(typeof(T), out var screen))
        {
            return screen as T;
        }
        return null;
    }

    public bool TryShowScreenByType(Type screenType)
    {
        BaseLog.Log($"[UIManager] TryShowScreenByType: {screenType.Name}");
        if (TryGetOrCreateScreen(screenType, out var screen))
        {
            screen.Show();
            return true;
        }
        return false;
    }

    public void HideScreen<T>() where T : BaseScreen
    {
        BaseLog.Log($"[UIManager] HideScreen: {typeof(T).Name}");
        if (screens.TryGetValue(typeof(T), out var screen))
        {
            screen.Hide();
            if (ShouldDestroyAfterHide(screen))
            {
                UnregisterScreen(screen);
                screen.Destroy();
            }
        }
    }

    public bool TryHideScreenByType(Type screenType)
    {
        BaseLog.Log($"[UIManager] TryHideScreenByType: {screenType.Name}");
        if (screens.TryGetValue(screenType, out var screen))
        {
            screen.Hide();
            if (ShouldDestroyAfterHide(screen))
            {
                UnregisterScreen(screen);
                screen.Destroy();
            }
            return true;
        }
        return false;
    }

    public T GetScreen<T>() where T : BaseScreen
    {
        return screens.TryGetValue(typeof(T), out var screen) ? screen as T : null;
    }

    public void ClearScreens()
    {
        screens.Clear();
    }

    public void LockInput(bool isLock)
    {
        BaseLog.Log($"[UIManager] LockInput: {isLock}");
        if (lockCanvas != null)
        {
            lockCanvas.gameObject.SetActive(isLock);
        }
    }

    public void NotifyPopupShown(BasePopup popup)
    {
        PopupShown?.Invoke(popup);
    }

    public void NotifyPopupHidden(BasePopup popup)
    {
        PopupHidden?.Invoke(popup);

        if (popup != null && ShouldDestroyAfterHide(popup))
        {
            UnregisterPopup(popup);
            popup.Destroy();
        }
    }

    private GameObject InstantiateWithVContainer(GameObject prefab, Transform parent)
    {
        if (objectResolver != null)
        {
            return objectResolver.Instantiate(prefab, parent);
        }
        return Instantiate(prefab, parent, false);
    }

    private bool TryGetOrCreatePopup(Type type, out BasePopup popup)
    {
        if (popups.TryGetValue(type, out popup) && popup != null)
        {
            return true;
        }

        if (popupCatalogEntries.TryGetValue(type, out var entry) && entry.Prefab != null)
        {
            GameObject instance = InstantiateWithVContainer(entry.Prefab, popupCanvas);
            popup = instance != null ? instance.GetComponent<BasePopup>() : null;
            if (popup != null)
            {
                popup.gameObject.SetActive(false);
                RegisterPopup(popup);
                popup.Initialize();
                return true;
            }
        }

        popup = null;
        return false;
    }

    private bool TryGetOrCreateScreen(Type type, out BaseScreen screen)
    {
        if (screens.TryGetValue(type, out screen) && screen != null)
        {
            return true;
        }

        if (screenCatalogEntries.TryGetValue(type, out var entry) && entry.Prefab != null)
        {
            GameObject instance = InstantiateWithVContainer(entry.Prefab, screenCanvas);
            screen = instance != null ? instance.GetComponent<BaseScreen>() : null;
            if (screen != null)
            {
                screen.gameObject.SetActive(false);
                RegisterScreen(screen);
                screen.Initialize();
                return true;
            }
        }

        screen = null;
        return false;
    }

    private bool ShouldDestroyAfterHide(BasePopup popup)
    {
        return popupCatalogEntries.TryGetValue(popup.GetType(), out var entry) && !entry.CacheAfterClose;
    }

    private bool ShouldDestroyAfterHide(BaseScreen screen)
    {
        return screenCatalogEntries.TryGetValue(screen.GetType(), out var entry) && !entry.CacheAfterClose;
    }

    public void ShowToast(string message)
    {
        BaseLog.Log($"[UIManager] ShowToast: {message}");
        if (toastPrefab == null)
        {
            BaseLog.LogWarning("[UIManager] Toast prefab is not assigned in the UIManager Inspector!");
            return;
        }

        Transform parent = notiCanvas != null ? notiCanvas : transform;
        ToastUI toast = null;
        if (poolService != null)
        {
            toast = poolService.Get(toastPrefab, parent);
        }
        else
        {
            toast = InstantiateWithVContainer(toastPrefab.gameObject, parent).GetComponent<ToastUI>();
        }

        if (toast != null)
        {
            toast.transform.SetAsLastSibling();
            toast.Show(message);
        }
    }
}
