using System;
using UnityEngine;

namespace HP.Framework.UI
{
    /// <summary>
    /// Contract for UI management operations (Screens, Popups, Toast notifications).
    /// </summary>
    public interface IUIService
    {
        Camera UICamera { get; }
        bool HasVisiblePopup { get; }

        event Action<BasePopup> PopupOpening;
        event Action<BasePopup> PopupShown;
        event Action<BasePopup> PopupHidden;

        void RegisterPopup(BasePopup popup);
        void UnregisterPopup(BasePopup popup);
        void RegisterScreen(BaseScreen screen);
        void UnregisterScreen(BaseScreen screen);

        T OpenPopup<T>() where T : BasePopup;
        T OpenPopup<T>(Action<T> configureBeforeShow) where T : BasePopup;
        BasePopup OpenPopup(Type type);
        T GetOrCreatePopup<T>() where T : BasePopup;
        void ClosePopup<T>() where T : BasePopup;
        T GetPopup<T>() where T : BasePopup;
        bool IsPopupRegistered<T>() where T : BasePopup;
        void CloseAllPopups(Action onCompleted = null);
        void ClearPopups();

        T ShowScreen<T>() where T : BaseScreen;
        T ShowScreen<T>(Action<T> configureBeforeShow) where T : BaseScreen;
        T GetOrCreateScreen<T>() where T : BaseScreen;
        bool TryShowScreenByType(Type screenType);
        void HideScreen<T>() where T : BaseScreen;
        bool TryHideScreenByType(Type screenType);
        T GetScreen<T>() where T : BaseScreen;
        void ClearScreens();

        void LockInput(bool isLock);
        void ShowToast(string message);
        void EnqueuePopup<T>(object data = null) where T : BasePopup;

        void NotifyPopupOpening(BasePopup popup);
        void NotifyPopupShown(BasePopup popup);
        void NotifyPopupHidden(BasePopup popup);
    }

    /// <summary>
    /// Marker for the application-level UI service owned by RootLifetimeScope. Child-scope UI
    /// services depend on this interface for the persistent UI roots without resolving another
    /// IUIService and creating a circular dependency.
    /// </summary>
    public interface IGlobalUIService : IUIService
    {
        Transform ScreenRoot { get; }
        Transform PopupRoot { get; }
        Transform NotificationRoot { get; }

        void AttachUICameraTo(Camera mainCamera);
    }

    /// <summary>
    /// UI service owned by a scene/feature LifetimeScope. Local prefabs are instantiated through
    /// that scope's resolver and destroyed when the scope is disposed. Unregistered type-based
    /// requests fall back to the application UI for backward compatibility.
    /// </summary>
    public interface IScopedUIService : IUIService, IDisposable
    {
        void RegisterPopupPrefab<T>(T prefab, bool cacheAfterClose = true)
            where T : BasePopup;
        void RegisterScreenPrefab<T>(T prefab, bool cacheAfterClose = true)
            where T : BaseScreen;

        T OpenPopup<T>(
            T prefab,
            Action<T> configureBeforeShow = null,
            bool cacheAfterClose = true)
            where T : BasePopup;

        T ShowScreen<T>(
            T prefab,
            Action<T> configureBeforeShow = null,
            bool cacheAfterClose = true)
            where T : BaseScreen;
    }
}

