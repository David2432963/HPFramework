using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace HP.Framework.UI
{
    /// <summary>
    /// Scene/feature-owned UI service. It reuses the persistent roots from IGlobalUIService but
    /// instantiates local views with the current child resolver, allowing those views to inject
    /// scene/feature-scoped dependencies safely.
    /// </summary>
    public sealed class ScopedUIService : IScopedUIService
    {
        private sealed class PopupRegistration
        {
            public BasePopup Prefab;
            public bool CacheAfterClose;
        }

        private sealed class ScreenRegistration
        {
            public BaseScreen Prefab;
            public bool CacheAfterClose;
        }

        private readonly IObjectResolver resolver;
        private readonly IGlobalUIService globalUi;
        private readonly Dictionary<Type, PopupRegistration> popupRegistrations =
            new Dictionary<Type, PopupRegistration>();
        private readonly Dictionary<Type, ScreenRegistration> screenRegistrations =
            new Dictionary<Type, ScreenRegistration>();
        private readonly Dictionary<Type, BasePopup> popups =
            new Dictionary<Type, BasePopup>();
        private readonly Dictionary<Type, BaseScreen> screens =
            new Dictionary<Type, BaseScreen>();
        private readonly List<BasePopup> activePopups = new List<BasePopup>();

        private bool disposed;

        public ScopedUIService(IObjectResolver resolver, IGlobalUIService globalUi)
        {
            this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            this.globalUi = globalUi ?? throw new ArgumentNullException(nameof(globalUi));
        }

        public Camera UICamera => globalUi.UICamera;

        public bool HasVisiblePopup
        {
            get
            {
                foreach (BasePopup popup in popups.Values)
                {
                    if (popup != null && popup.gameObject.activeSelf)
                    {
                        return true;
                    }
                }

                return globalUi.HasVisiblePopup;
            }
        }

        public event Action<BasePopup> PopupOpening;
        public event Action<BasePopup> PopupShown;
        public event Action<BasePopup> PopupHidden;

        public void RegisterPopupPrefab<T>(T prefab, bool cacheAfterClose = true)
            where T : BasePopup
        {
            ThrowIfDisposed();
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            popupRegistrations[typeof(T)] = new PopupRegistration
            {
                Prefab = prefab,
                CacheAfterClose = cacheAfterClose
            };
        }

        public void RegisterScreenPrefab<T>(T prefab, bool cacheAfterClose = true)
            where T : BaseScreen
        {
            ThrowIfDisposed();
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            screenRegistrations[typeof(T)] = new ScreenRegistration
            {
                Prefab = prefab,
                CacheAfterClose = cacheAfterClose
            };
        }

        public T OpenPopup<T>(
            T prefab,
            Action<T> configureBeforeShow = null,
            bool cacheAfterClose = true)
            where T : BasePopup
        {
            RegisterPopupPrefab(prefab, cacheAfterClose);
            return OpenPopup(configureBeforeShow);
        }

        public T ShowScreen<T>(
            T prefab,
            Action<T> configureBeforeShow = null,
            bool cacheAfterClose = true)
            where T : BaseScreen
        {
            RegisterScreenPrefab(prefab, cacheAfterClose);
            return ShowScreen(configureBeforeShow);
        }

        public void RegisterPopup(BasePopup popup)
        {
            ThrowIfDisposed();
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
            ThrowIfDisposed();
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
            ThrowIfDisposed();
            if (TryGetOrCreateLocalPopup(typeof(T), out BasePopup popup))
            {
                T typedPopup = popup as T;
                configureBeforeShow?.Invoke(typedPopup);
                typedPopup?.Show();
                return typedPopup;
            }

            return globalUi.OpenPopup<T>(configureBeforeShow);
        }

        public BasePopup OpenPopup(Type type)
        {
            ThrowIfDisposed();
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (TryGetOrCreateLocalPopup(type, out BasePopup popup))
            {
                popup.Show();
                return popup;
            }

            return globalUi.OpenPopup(type);
        }

        public T GetOrCreatePopup<T>() where T : BasePopup
        {
            ThrowIfDisposed();
            return TryGetOrCreateLocalPopup(typeof(T), out BasePopup popup)
                ? popup as T
                : globalUi.GetOrCreatePopup<T>();
        }

        public void ClosePopup<T>() where T : BasePopup
        {
            ThrowIfDisposed();
            if (popups.TryGetValue(typeof(T), out BasePopup popup) && popup != null)
            {
                popup.Hide();
                return;
            }

            globalUi.ClosePopup<T>();
        }

        public T GetPopup<T>() where T : BasePopup
        {
            ThrowIfDisposed();
            return popups.TryGetValue(typeof(T), out BasePopup popup) && popup != null
                ? popup as T
                : globalUi.GetPopup<T>();
        }

        public bool IsPopupRegistered<T>() where T : BasePopup
        {
            ThrowIfDisposed();
            return popups.ContainsKey(typeof(T))
                || popupRegistrations.ContainsKey(typeof(T))
                || globalUi.IsPopupRegistered<T>();
        }

        public void CloseAllPopups(Action onCompleted = null)
        {
            ThrowIfDisposed();
            activePopups.Clear();
            foreach (BasePopup popup in popups.Values)
            {
                if (popup != null && popup.gameObject.activeSelf)
                {
                    activePopups.Add(popup);
                }
            }

            if (activePopups.Count == 0)
            {
                onCompleted?.Invoke();
                return;
            }

            int remaining = activePopups.Count;
            void HandleHidden()
            {
                remaining--;
                if (remaining == 0)
                {
                    onCompleted?.Invoke();
                }
            }

            for (int i = 0; i < activePopups.Count; i++)
            {
                activePopups[i].Hide(HandleHidden);
            }

            activePopups.Clear();
        }

        public void ClearPopups()
        {
            ThrowIfDisposed();
            DestroyLocalPopups();
        }

        public T ShowScreen<T>() where T : BaseScreen
        {
            return ShowScreen<T>(null);
        }

        public T ShowScreen<T>(Action<T> configureBeforeShow) where T : BaseScreen
        {
            ThrowIfDisposed();
            if (TryGetOrCreateLocalScreen(typeof(T), out BaseScreen screen))
            {
                T typedScreen = screen as T;
                configureBeforeShow?.Invoke(typedScreen);
                typedScreen?.Show();
                return typedScreen;
            }

            return globalUi.ShowScreen<T>(configureBeforeShow);
        }

        public T GetOrCreateScreen<T>() where T : BaseScreen
        {
            ThrowIfDisposed();
            return TryGetOrCreateLocalScreen(typeof(T), out BaseScreen screen)
                ? screen as T
                : globalUi.GetOrCreateScreen<T>();
        }

        public bool TryShowScreenByType(Type screenType)
        {
            ThrowIfDisposed();
            if (screenType == null)
            {
                return false;
            }

            if (TryGetOrCreateLocalScreen(screenType, out BaseScreen screen))
            {
                screen.Show();
                return true;
            }

            return globalUi.TryShowScreenByType(screenType);
        }

        public void HideScreen<T>() where T : BaseScreen
        {
            ThrowIfDisposed();
            if (screens.TryGetValue(typeof(T), out BaseScreen screen) && screen != null)
            {
                HideLocalScreen(screen);
                return;
            }

            globalUi.HideScreen<T>();
        }

        public bool TryHideScreenByType(Type screenType)
        {
            ThrowIfDisposed();
            if (screenType != null
                && screens.TryGetValue(screenType, out BaseScreen screen)
                && screen != null)
            {
                HideLocalScreen(screen);
                return true;
            }

            return globalUi.TryHideScreenByType(screenType);
        }

        public T GetScreen<T>() where T : BaseScreen
        {
            ThrowIfDisposed();
            return screens.TryGetValue(typeof(T), out BaseScreen screen) && screen != null
                ? screen as T
                : globalUi.GetScreen<T>();
        }

        public void ClearScreens()
        {
            ThrowIfDisposed();
            DestroyLocalScreens();
        }

        public void LockInput(bool isLock)
        {
            globalUi.LockInput(isLock);
        }

        public void ShowToast(string message)
        {
            globalUi.ShowToast(message);
        }

        public void EnqueuePopup<T>(object data = null) where T : BasePopup
        {
            OpenPopup<T>();
        }

        public void NotifyPopupOpening(BasePopup popup)
        {
            if (popup != null && popups.ContainsKey(popup.GetType()))
            {
                PopupOpening?.Invoke(popup);
            }
        }

        public void NotifyPopupShown(BasePopup popup)
        {
            if (popup != null && popups.ContainsKey(popup.GetType()))
            {
                PopupShown?.Invoke(popup);
            }
        }

        public void NotifyPopupHidden(BasePopup popup)
        {
            if (popup == null || !popups.ContainsKey(popup.GetType()))
            {
                return;
            }

            PopupHidden?.Invoke(popup);
            if (ShouldDestroyAfterHide(popup))
            {
                UnregisterPopup(popup);
                popup.Destroy();
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            DestroyLocalPopups();
            DestroyLocalScreens();
            popupRegistrations.Clear();
            screenRegistrations.Clear();
            PopupOpening = null;
            PopupShown = null;
            PopupHidden = null;
            disposed = true;
        }

        private bool TryGetOrCreateLocalPopup(Type type, out BasePopup popup)
        {
            if (popups.TryGetValue(type, out popup) && popup != null)
            {
                return true;
            }

            if (!popupRegistrations.TryGetValue(type, out PopupRegistration registration)
                || registration.Prefab == null)
            {
                popup = null;
                return false;
            }

            GameObject instance = resolver.Instantiate(
                registration.Prefab.gameObject,
                globalUi.PopupRoot);
            popup = instance != null ? instance.GetComponent(type) as BasePopup : null;
            if (popup == null)
            {
                if (instance != null)
                {
                    UnityEngine.Object.Destroy(instance);
                }

                return false;
            }

            popup.gameObject.SetActive(false);
            RegisterPopup(popup);
            popup.Initialize();
            return true;
        }

        private bool TryGetOrCreateLocalScreen(Type type, out BaseScreen screen)
        {
            if (screens.TryGetValue(type, out screen) && screen != null)
            {
                return true;
            }

            if (!screenRegistrations.TryGetValue(type, out ScreenRegistration registration)
                || registration.Prefab == null)
            {
                screen = null;
                return false;
            }

            GameObject instance = resolver.Instantiate(
                registration.Prefab.gameObject,
                globalUi.ScreenRoot);
            screen = instance != null ? instance.GetComponent(type) as BaseScreen : null;
            if (screen == null)
            {
                if (instance != null)
                {
                    UnityEngine.Object.Destroy(instance);
                }

                return false;
            }

            screen.gameObject.SetActive(false);
            RegisterScreen(screen);
            screen.Initialize();
            return true;
        }

        private bool ShouldDestroyAfterHide(BasePopup popup)
        {
            return popupRegistrations.TryGetValue(
                    popup.GetType(),
                    out PopupRegistration registration)
                && !registration.CacheAfterClose;
        }

        private void HideLocalScreen(BaseScreen screen)
        {
            screen.Hide();
            if (screenRegistrations.TryGetValue(
                    screen.GetType(),
                    out ScreenRegistration registration)
                && !registration.CacheAfterClose)
            {
                UnregisterScreen(screen);
                screen.Destroy();
            }
        }

        private void DestroyLocalPopups()
        {
            foreach (BasePopup popup in popups.Values)
            {
                if (popup != null)
                {
                    popup.Destroy();
                }
            }

            popups.Clear();
            activePopups.Clear();
        }

        private void DestroyLocalScreens()
        {
            foreach (BaseScreen screen in screens.Values)
            {
                if (screen != null)
                {
                    screen.Destroy();
                }
            }

            screens.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(ScopedUIService));
            }
        }
    }
}


