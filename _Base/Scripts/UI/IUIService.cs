using System;
using UnityEngine;

namespace Base.UI
{
    /// <summary>
    /// Contract for UI management operations (Screens, Popups, Toast notifications).
    /// </summary>
    public interface IUIService
    {
        Camera UICamera { get; }
        bool HasVisiblePopup { get; }

        event Action<BasePopup> PopupShown;
        event Action<BasePopup> PopupHidden;

        void RegisterPopup(BasePopup popup);
        void UnregisterPopup(BasePopup popup);
        void RegisterScreen(BaseScreen screen);
        void UnregisterScreen(BaseScreen screen);

        T OpenPopup<T>() where T : BasePopup;
        BasePopup OpenPopup(Type type);
        T GetOrCreatePopup<T>() where T : BasePopup;
        void ClosePopup<T>() where T : BasePopup;
        T GetPopup<T>() where T : BasePopup;
        bool IsPopupRegistered<T>() where T : BasePopup;
        void CloseAllPopups();
        void ClearPopups();

        T ShowScreen<T>() where T : BaseScreen;
        T GetOrCreateScreen<T>() where T : BaseScreen;
        bool TryShowScreenByType(Type screenType);
        void HideScreen<T>() where T : BaseScreen;
        bool TryHideScreenByType(Type screenType);
        T GetScreen<T>() where T : BaseScreen;
        void ClearScreens();

        void LockInput(bool isLock);
        void ShowToast(string message);

        void NotifyPopupShown(BasePopup popup);
        void NotifyPopupHidden(BasePopup popup);
    }
}
