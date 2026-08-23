namespace HP.Framework.UI
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using HP.Framework.Assets;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.Serialization;
    using HP.Framework.Pooling;
    using HP.Framework;
    using VContainer;
    using VContainer.Unity;

    /// <summary>
    /// UI Manager for screens and popups.
    /// Implements IUIService with VContainer native atomic instantiation (Instantiate + Inject in 1 step).
    /// Uses only Unity runtime dependencies; URP camera stacking is attached through reflection when available.
    /// </summary>
    public class UIManager : MonoBehaviour, IGlobalUIService, IInitializable
    {
        private sealed class PendingUICreation
        {
            public PendingUICreation(Type type, UICatalogSO.UIEntry entry, bool isPopup)
            {
                Type = type;
                Entry = entry;
                IsPopup = isPopup;
                Completion = new UniTaskCompletionSource<Component>();
            }

            public Type Type { get; }
            public UICatalogSO.UIEntry Entry { get; }
            public bool IsPopup { get; }
            public UniTaskCompletionSource<Component> Completion { get; }
            public int WaiterCount { get; set; }
            public bool Completed { get; set; }
            public bool Claimed { get; set; }
            public bool Invalidated { get; set; }
            public GameObject Instance { get; set; }
            public Component View { get; set; }
            public IAssetLease<GameObject> Lease { get; set; }
        }

        [Header("UI Roots & Camera")]
        [FormerlySerializedAs("screenCanvas")]
        [SerializeField] private RectTransform screenRoot;
        [FormerlySerializedAs("popupCanvas")]
        [SerializeField] private RectTransform popupRoot;
        [FormerlySerializedAs("notiCanvas")]
        [SerializeField] private RectTransform notificationRoot;
        [FormerlySerializedAs("lockCanvas")]
        [SerializeField] private RectTransform inputBlocker;
        [SerializeField] private Camera uiCamera;

        [Header("UI Catalog & Prefabs")]
        [SerializeField] private UICatalogSO uiCatalog;
        [SerializeField] private ToastUI toastPrefab;

        private readonly Dictionary<Type, BasePopup> popups = new Dictionary<Type, BasePopup>();
        private readonly Dictionary<Type, BaseScreen> screens = new Dictionary<Type, BaseScreen>();
        private readonly Dictionary<Type, UICatalogSO.UIEntry> popupCatalogEntries = new Dictionary<Type, UICatalogSO.UIEntry>();
        private readonly Dictionary<Type, UICatalogSO.UIEntry> screenCatalogEntries = new Dictionary<Type, UICatalogSO.UIEntry>();
        private readonly List<BasePopup> activePopupsCache = new List<BasePopup>();
        private readonly List<BaseScreen> screensToDestroyCache = new List<BaseScreen>();
        private readonly Dictionary<Type, IAssetLease<GameObject>> viewAssetLeases =
            new Dictionary<Type, IAssetLease<GameObject>>();
        private readonly Dictionary<Type, PendingUICreation> pendingCreations =
            new Dictionary<Type, PendingUICreation>();

        private IPoolService poolService;
        private IObjectResolver objectResolver;
        private IAssetLeaseProvider assetLeaseProvider;
        private bool initialized;

        public event Action<BasePopup> PopupOpening;
        public event Action<BasePopup> PopupShown;
        public event Action<BasePopup> PopupHidden;

        public Camera UICamera => uiCamera;
        public Transform ScreenRoot => screenRoot != null ? screenRoot : transform;
        public Transform PopupRoot => popupRoot != null ? popupRoot : transform;
        public Transform NotificationRoot => notificationRoot != null ? notificationRoot : transform;

        [Inject]
        public void Construct(
            IPoolService poolService,
            IObjectResolver objectResolver,
            IAssetLeaseProvider assetLeaseProvider)
        {
            this.poolService = poolService;
            this.objectResolver = objectResolver;
            this.assetLeaseProvider = assetLeaseProvider;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            ClearPopups();
            ClearScreens();
            CancelPendingCreations();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AttachUICameraToMainCamera();
        }

        private void AttachUICameraToMainCamera()
        {
            AttachUICameraTo(Camera.main);
        }

        public void AttachUICameraTo(Camera baseCamera)
        {
            if (uiCamera == null)
            {
                return;
            }

            URPCameraStackUtility.ConfigureAsOverlay(uiCamera);

            if (baseCamera == null)
            {
                return;
            }

            URPCameraStackUtility.AttachOverlay(baseCamera, uiCamera);
        }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

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

                PreloadCatalogEntries();
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

            CancelPendingCreations();
            ClearPopups();
            ClearScreens();
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
            return OpenPopup<T>(null);
        }

        public T OpenPopup<T>(Action<T> configureBeforeShow) where T : BasePopup
        {
            BaseLog.Log($"[UIManager] OpenPopup: {typeof(T).Name}");
            if (TryGetOrCreatePopup(typeof(T), out BasePopup popup)
                && popup is T typedPopup)
            {
                configureBeforeShow?.Invoke(typedPopup);
                typedPopup.Show();
                return typedPopup;
            }

            return null;
        }

        public UniTask<T> OpenPopupAsync<T>(CancellationToken cancellationToken = default)
            where T : BasePopup
        {
            return OpenPopupAsync<T>(null, cancellationToken);
        }

        public async UniTask<T> OpenPopupAsync<T>(
            Action<T> configureBeforeShow,
            CancellationToken cancellationToken = default)
            where T : BasePopup
        {
            EnsureInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetOrCreateDirectPopup(typeof(T), out BasePopup directPopup))
            {
                T typedDirect = directPopup as T;
                configureBeforeShow?.Invoke(typedDirect);
                typedDirect?.Show();
                return typedDirect;
            }

            Component created = await GetOrCreateAssetViewAsync(
                typeof(T),
                isPopup: true,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            T popup = created as T;
            configureBeforeShow?.Invoke(popup);
            popup?.Show();
            return popup;
        }

        public T GetOrCreatePopup<T>() where T : BasePopup
        {
            return TryGetOrCreatePopup(typeof(T), out BasePopup popup)
                ? popup as T
                : null;
        }

        public BasePopup OpenPopup(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

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

        public void CloseAllPopups(Action onCompleted = null)
        {
            activePopupsCache.Clear();
            foreach (var popup in popups.Values)
            {
                if (popup != null && popup.gameObject.activeSelf)
                {
                    activePopupsCache.Add(popup);
                }
            }

            if (activePopupsCache.Count == 0)
            {
                onCompleted?.Invoke();
                return;
            }

            int remaining = activePopupsCache.Count;
            void HandlePopupHidden()
            {
                remaining--;
                if (remaining == 0)
                {
                    onCompleted?.Invoke();
                }
            }

            for (int i = 0; i < activePopupsCache.Count; i++)
            {
                BasePopup popup = activePopupsCache[i];
                if (popup != null && popup.gameObject.activeSelf)
                {
                    popup.Hide(HandlePopupHidden);
                }
            }
            activePopupsCache.Clear();
        }

        public void ClearPopups()
        {
            activePopupsCache.Clear();
            foreach (BasePopup popup in popups.Values)
            {
                if (popup != null)
                {
                    activePopupsCache.Add(popup);
                }
            }

            // Clear ownership first so a custom Destroy implementation cannot mutate the
            // dictionary while it is being enumerated.
            popups.Clear();
            for (int i = 0; i < activePopupsCache.Count; i++)
            {
                BasePopup popup = activePopupsCache[i];
                if (popup.gameObject.activeSelf)
                {
                    popup.gameObject.SetActive(false);
                }
                DestroyOwnedPopup(popup);
            }
            activePopupsCache.Clear();
            foreach (Type type in popupCatalogEntries.Keys)
            {
                ReleaseViewAssetLease(type);
            }
        }

        public T ShowScreen<T>() where T : BaseScreen
        {
            return ShowScreen<T>(null);
        }

        public T ShowScreen<T>(Action<T> configureBeforeShow) where T : BaseScreen
        {
            BaseLog.Log($"[UIManager] ShowScreen: {typeof(T).Name}");
            if (TryGetOrCreateScreen(typeof(T), out BaseScreen screen)
                && screen is T typedScreen)
            {
                configureBeforeShow?.Invoke(typedScreen);
                typedScreen.Show();
                return typedScreen;
            }

            return null;
        }

        public UniTask<T> ShowScreenAsync<T>(CancellationToken cancellationToken = default)
            where T : BaseScreen
        {
            return ShowScreenAsync<T>(null, cancellationToken);
        }

        public async UniTask<T> ShowScreenAsync<T>(
            Action<T> configureBeforeShow,
            CancellationToken cancellationToken = default)
            where T : BaseScreen
        {
            EnsureInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetOrCreateDirectScreen(typeof(T), out BaseScreen directScreen))
            {
                T typedDirect = directScreen as T;
                configureBeforeShow?.Invoke(typedDirect);
                typedDirect?.Show();
                return typedDirect;
            }

            Component created = await GetOrCreateAssetViewAsync(
                typeof(T),
                isPopup: false,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            T screen = created as T;
            configureBeforeShow?.Invoke(screen);
            screen?.Show();
            return screen;
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
            if (screenType == null)
            {
                return false;
            }

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
                    DestroyOwnedScreen(screen);
                }
            }
        }

        public bool TryHideScreenByType(Type screenType)
        {
            if (screenType == null)
            {
                return false;
            }

            BaseLog.Log($"[UIManager] TryHideScreenByType: {screenType.Name}");
            if (screens.TryGetValue(screenType, out var screen))
            {
                screen.Hide();
                if (ShouldDestroyAfterHide(screen))
                {
                    UnregisterScreen(screen);
                    DestroyOwnedScreen(screen);
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
            screensToDestroyCache.Clear();
            foreach (BaseScreen screen in screens.Values)
            {
                if (screen != null)
                {
                    screensToDestroyCache.Add(screen);
                }
            }

            screens.Clear();
            for (int i = 0; i < screensToDestroyCache.Count; i++)
            {
                BaseScreen screen = screensToDestroyCache[i];
                if (screen.gameObject.activeSelf)
                {
                    screen.gameObject.SetActive(false);
                }
                DestroyOwnedScreen(screen);
            }
            screensToDestroyCache.Clear();
            foreach (Type type in screenCatalogEntries.Keys)
            {
                ReleaseViewAssetLease(type);
            }
        }

        public void LockInput(bool isLock)
        {
            BaseLog.Log($"[UIManager] LockInput: {isLock}");
            if (inputBlocker != null)
            {
                inputBlocker.gameObject.SetActive(isLock);
            }
        }

        public void EnqueuePopup<T>(object data = null) where T : BasePopup
        {
            OpenPopup<T>();
        }

        public void NotifyPopupOpening(BasePopup popup)
        {
            PopupOpening?.Invoke(popup);
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
                DestroyOwnedPopup(popup);
            }
        }

        private void PreloadCatalogEntries()
        {
            foreach (var entry in uiCatalog.GetPreloadEntries())
            {
                if (entry == null || entry.Prefab == null || !entry.TryGetRuntimeType(out var type))
                {
                    continue;
                }

                if (popups.ContainsKey(type) || screens.ContainsKey(type))
                {
                    continue;
                }

                bool isScreen = typeof(BaseScreen).IsAssignableFrom(type);
                Transform parent = isScreen ? ScreenRoot : PopupRoot;
                GameObject instance = InstantiateWithVContainer(entry.Prefab, parent);
                if (instance == null)
                {
                    continue;
                }

                instance.SetActive(false);
                if (isScreen)
                {
                    BaseScreen screen = instance.GetComponent<BaseScreen>();
                    if (screen != null)
                    {
                        RegisterScreen(screen);
                        screen.Initialize();
                    }
                    else
                    {
                        Destroy(instance);
                    }
                }
                else
                {
                    BasePopup popup = instance.GetComponent<BasePopup>();
                    if (popup != null)
                    {
                        RegisterPopup(popup);
                        popup.Initialize();
                    }
                    else
                    {
                        Destroy(instance);
                    }
                }
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
            EnsureInitialized();
            if (popups.TryGetValue(type, out popup) && popup != null)
            {
                return true;
            }

            if (popupCatalogEntries.TryGetValue(type, out UICatalogSO.UIEntry entry)
                && entry.AssetMode == UIAssetMode.AssetKey)
            {
                throw CreateAsyncRequiredException(type, isPopup: true);
            }

            return TryGetOrCreateDirectPopup(type, out popup);
        }

        private bool TryGetOrCreateScreen(Type type, out BaseScreen screen)
        {
            EnsureInitialized();
            if (screens.TryGetValue(type, out screen) && screen != null)
            {
                return true;
            }

            if (screenCatalogEntries.TryGetValue(type, out UICatalogSO.UIEntry entry)
                && entry.AssetMode == UIAssetMode.AssetKey)
            {
                throw CreateAsyncRequiredException(type, isPopup: false);
            }

            return TryGetOrCreateDirectScreen(type, out screen);
        }

        private bool TryGetOrCreateDirectPopup(Type type, out BasePopup popup)
        {
            if (popups.TryGetValue(type, out popup) && popup != null)
            {
                return true;
            }

            if (!popupCatalogEntries.TryGetValue(type, out UICatalogSO.UIEntry entry)
                || entry.AssetMode != UIAssetMode.DirectPrefab
                || entry.Prefab == null)
            {
                popup = null;
                return false;
            }

            GameObject instance = InstantiateWithVContainer(entry.Prefab, PopupRoot);
            popup = instance != null ? instance.GetComponent<BasePopup>() : null;
            if (popup != null)
            {
                popup.gameObject.SetActive(false);
                RegisterPopup(popup);
                popup.Initialize();
                return true;
            }

            if (instance != null)
            {
                Destroy(instance);
            }

            return false;
        }

        private bool TryGetOrCreateDirectScreen(Type type, out BaseScreen screen)
        {
            if (screens.TryGetValue(type, out screen) && screen != null)
            {
                return true;
            }

            if (!screenCatalogEntries.TryGetValue(type, out UICatalogSO.UIEntry entry)
                || entry.AssetMode != UIAssetMode.DirectPrefab
                || entry.Prefab == null)
            {
                screen = null;
                return false;
            }

            GameObject instance = InstantiateWithVContainer(entry.Prefab, ScreenRoot);
            screen = instance != null ? instance.GetComponent<BaseScreen>() : null;
            if (screen != null)
            {
                screen.gameObject.SetActive(false);
                RegisterScreen(screen);
                screen.Initialize();
                return true;
            }

            if (instance != null)
            {
                Destroy(instance);
            }

            return false;
        }

        private async UniTask<Component> GetOrCreateAssetViewAsync(
            Type type,
            bool isPopup,
            CancellationToken cancellationToken)
        {
            if (isPopup
                && popups.TryGetValue(type, out BasePopup existingPopup)
                && existingPopup != null)
            {
                return existingPopup;
            }

            if (!isPopup
                && screens.TryGetValue(type, out BaseScreen existingScreen)
                && existingScreen != null)
            {
                return existingScreen;
            }

            Dictionary<Type, UICatalogSO.UIEntry> catalog = isPopup
                ? popupCatalogEntries
                : screenCatalogEntries;
            if (!catalog.TryGetValue(type, out UICatalogSO.UIEntry entry))
            {
                return null;
            }

            if (entry.AssetMode == UIAssetMode.DirectPrefab)
            {
                return isPopup
                    ? TryGetOrCreateDirectPopup(type, out BasePopup popup) ? popup : null
                    : TryGetOrCreateDirectScreen(type, out BaseScreen screen) ? screen : null;
            }

            if (assetLeaseProvider == null)
            {
                throw new InvalidOperationException(
                    $"UI type '{type.FullName}' uses AssetKey mode, but IAssetLeaseProvider is not configured.");
            }

            bool startCreation = false;
            if (!pendingCreations.TryGetValue(type, out PendingUICreation pending))
            {
                pending = new PendingUICreation(type, entry, isPopup);
                pendingCreations.Add(type, pending);
                startCreation = true;
            }

            pending.WaiterCount++;
            if (startCreation)
            {
                CreatePendingViewAsync(pending).Forget();
            }

            try
            {
                Component view = await pending.Completion.Task
                    .AttachExternalCancellation(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!pending.Claimed)
                {
                    ClaimPendingView(pending);
                }

                return view;
            }
            finally
            {
                if (pending.WaiterCount > 0)
                {
                    pending.WaiterCount--;
                }

                if (pending.Completed && pending.WaiterCount == 0 && !pending.Claimed)
                {
                    CleanupPendingView(pending);
                }
            }
        }

        private async UniTask CreatePendingViewAsync(PendingUICreation pending)
        {
            IAssetLease<GameObject> lease = null;
            GameObject instance = null;
            try
            {
                lease = await assetLeaseProvider.AcquireAsync<GameObject>(
                    pending.Entry.AssetKey,
                    CancellationToken.None);
                if (pending.Invalidated)
                {
                    lease?.Dispose();
                    return;
                }

                if (lease == null || !lease.IsValid || lease.Asset == null)
                {
                    throw new InvalidOperationException(
                        $"UI asset '{pending.Entry.AssetKey}' could not be loaded.");
                }

                Transform parent = pending.IsPopup ? PopupRoot : ScreenRoot;
                instance = InstantiateWithVContainer(lease.Asset, parent);
                Component view = instance != null ? instance.GetComponent(pending.Type) : null;
                if (view == null)
                {
                    throw new InvalidOperationException(
                        $"UI asset '{pending.Entry.AssetKey}' does not contain component '{pending.Type.FullName}'.");
                }

                instance.SetActive(false);
                pending.Lease = lease;
                pending.Instance = instance;
                pending.View = view;
                pending.Completed = true;
                pending.Completion.TrySetResult(view);
                if (pending.WaiterCount == 0)
                {
                    CleanupPendingView(pending);
                }
            }
            catch (Exception exception)
            {
                if (instance != null)
                {
                    Destroy(instance);
                }

                lease?.Dispose();
                pending.Completed = true;
                pending.Completion.TrySetException(exception);
                if (pending.WaiterCount == 0)
                {
                    CleanupPendingView(pending);
                }
            }
        }

        private void ClaimPendingView(PendingUICreation pending)
        {
            if (pending.Invalidated || pending.View == null)
            {
                throw new InvalidOperationException(
                    $"Pending UI creation for '{pending.Type.FullName}' is no longer valid.");
            }

            if (pending.IsPopup)
            {
                BasePopup popup = pending.View as BasePopup;
                RegisterPopup(popup);
                popup.Initialize();
            }
            else
            {
                BaseScreen screen = pending.View as BaseScreen;
                RegisterScreen(screen);
                screen.Initialize();
            }

            if (viewAssetLeases.TryGetValue(pending.Type, out IAssetLease<GameObject> oldLease))
            {
                oldLease.Dispose();
            }

            viewAssetLeases[pending.Type] = pending.Lease;
            pending.Lease = null;
            pending.Instance = null;
            pending.Claimed = true;
            pendingCreations.Remove(pending.Type);
        }

        private void CleanupPendingView(PendingUICreation pending)
        {
            if (pending.Instance != null)
            {
                Destroy(pending.Instance);
                pending.Instance = null;
            }

            pending.Lease?.Dispose();
            pending.Lease = null;
            if (pendingCreations.TryGetValue(pending.Type, out PendingUICreation current)
                && ReferenceEquals(current, pending))
            {
                pendingCreations.Remove(pending.Type);
            }
        }

        private void CancelPendingCreations()
        {
            while (pendingCreations.Count > 0)
            {
                PendingUICreation pending = null;
                foreach (PendingUICreation candidate in pendingCreations.Values)
                {
                    pending = candidate;
                    break;
                }

                pending.Invalidated = true;
                pending.Completed = true;
                pending.Completion.TrySetException(new InvalidOperationException(
                    $"UI catalog ownership changed while '{pending.Type.FullName}' was loading."));
                CleanupPendingView(pending);
            }
        }

        private void DestroyOwnedPopup(BasePopup popup)
        {
            Type type = popup.GetType();
            popup.Destroy();
            ReleaseViewAssetLease(type);
        }

        private void DestroyOwnedScreen(BaseScreen screen)
        {
            Type type = screen.GetType();
            screen.Destroy();
            ReleaseViewAssetLease(type);
        }

        private void ReleaseViewAssetLease(Type type)
        {
            if (viewAssetLeases.TryGetValue(type, out IAssetLease<GameObject> lease))
            {
                viewAssetLeases.Remove(type);
                lease?.Dispose();
            }
        }

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                Initialize();
            }
        }

        private static InvalidOperationException CreateAsyncRequiredException(
            Type type,
            bool isPopup)
        {
            string method = isPopup ? "OpenPopupAsync<T>()" : "ShowScreenAsync<T>()";
            return new InvalidOperationException(
                $"UI type '{type.FullName}' uses AssetKey mode. Use {method} instead of the synchronous API.");
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

            Transform parent = NotificationRoot;
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


}
