namespace HP.Framework.UI
{
    using System;
    using System.Collections;
    using HP.Framework;
    using HP.Framework.UI;
    using UnityEngine;
    using VContainer;

    /// <summary>
    /// Base class for popup UI that may be closed by back button.
    /// Runs independent backdrop/content transitions while preserving VContainer UI service integration.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class BasePopup : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] protected UIAnimationPresetSO animationPreset;

        [Header("Animation Targets")]
        [SerializeField] private CanvasGroup backdropCanvasGroup;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private CanvasGroup contentCanvasGroup;

        private CanvasGroup canvasGroup;
        private PopupTransitionPlayer transitionPlayer;
        private Coroutine activeTransition;
        private Action hideCompleted;
        private PopupTransitionState transitionState = PopupTransitionState.Hidden;

        protected IUIService uiService;

        [Inject]
        public void Construct(IUIService uiService)
        {
            this.uiService = uiService;
        }

        protected CanvasGroup CanvasGroup
        {
            get
            {
                if (canvasGroup == null)
                {
                    TryGetComponent(out canvasGroup);
                }

                return canvasGroup;
            }
        }

        /// <summary>
        /// Override only to cache internal components or bind local UI listeners.
        /// External services and passed data should be consumed in Initialize/OnShow instead.
        /// </summary>
        protected virtual void Awake()
        {
            TryGetComponent(out canvasGroup);
            if (animationPreset == null)
            {
                return;
            }

            transitionPlayer = new PopupTransitionPlayer(
                backdropCanvasGroup,
                contentRoot,
                contentCanvasGroup,
                animationPreset);
        }

        public virtual void Show()
        {
            BaseLog.Log($"[UI] Show Popup: {gameObject.name}");

            PopupTransitionState previousState = transitionState;
            gameObject.SetActive(true);

            hideCompleted = null;
            StopActiveTransition();

            CanvasGroup.alpha = 1f;
            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = true;

            transitionState = PopupTransitionState.Showing;
            uiService?.NotifyPopupOpening(this);

            // Refresh/setup popup data before animation reveals the final state.
            OnShow();

            if (transitionPlayer == null)
            {
                CompleteShow();
                return;
            }

            bool preserveCurrentState =
                previousState == PopupTransitionState.Showing
                || previousState == PopupTransitionState.Hiding;
            if (transitionPlayer.TryPlayShow(
                preserveCurrentState,
                CompleteShow,
                out float duration))
            {
                return;
            }

            if (duration <= 0f)
            {
                transitionPlayer.ApplyShow(duration);
                CompleteShow();
                return;
            }

            activeTransition = StartCoroutine(PlayTransition(true, duration));
        }

        public virtual void Hide()
        {
            Hide(null);
        }

        public void Hide(Action onCompleted)
        {
            BaseLog.Log($"[UI] Hide Popup: {gameObject.name}");

            if (!gameObject.activeSelf)
            {
                onCompleted?.Invoke();
                return;
            }

            if (onCompleted != null)
            {
                hideCompleted += onCompleted;
            }

            if (transitionState == PopupTransitionState.Hiding)
            {
                return;
            }

            StopActiveTransition();
            OnHide();

            CanvasGroup.interactable = false;
            // Keep blocking until the closing animation is complete so input cannot leak through.
            CanvasGroup.blocksRaycasts = true;
            transitionState = PopupTransitionState.Hiding;

            if (transitionPlayer == null)
            {
                CompleteHide();
                return;
            }

            if (transitionPlayer.TryPlayHide(CompleteHide, out float duration))
            {
                return;
            }

            if (duration <= 0f)
            {
                transitionPlayer.ApplyHide(duration);
                CompleteHide();
                return;
            }

            activeTransition = StartCoroutine(PlayTransition(false, duration));
        }

        private IEnumerator PlayTransition(bool show, float duration)
        {
            float elapsed = 0f;
            ApplyTransition(show, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplyTransition(show, Mathf.Min(elapsed, duration));
                yield return null;
            }

            activeTransition = null;
            if (show)
            {
                CompleteShow();
            }
            else
            {
                CompleteHide();
            }
        }

        private void ApplyTransition(bool show, float elapsed)
        {
            if (show)
            {
                transitionPlayer.ApplyShow(elapsed);
            }
            else
            {
                transitionPlayer.ApplyHide(elapsed);
            }
        }

        private void CompleteShow()
        {
            activeTransition = null;
            transitionState = PopupTransitionState.Shown;
            transitionPlayer?.ApplyShownState();

            CanvasGroup.alpha = 1f;
            CanvasGroup.interactable = true;
            CanvasGroup.blocksRaycasts = true;

            OnShown();
            uiService?.NotifyPopupShown(this);
        }

        private void CompleteHide()
        {
            activeTransition = null;
            transitionState = PopupTransitionState.Hidden;

            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = false;
            transitionPlayer?.ApplyShownState();
            CanvasGroup.alpha = 1f;

            Action completed = hideCompleted;
            hideCompleted = null;

            gameObject.SetActive(false);
            OnClose();
            uiService?.NotifyPopupHidden(this);
            completed?.Invoke();
        }

        private void StopActiveTransition()
        {
            if (activeTransition == null)
            {
                transitionPlayer?.StopPlayback();
                return;
            }

            StopCoroutine(activeTransition);
            activeTransition = null;
            transitionPlayer?.StopPlayback();
        }

        public void Close()
        {
            Hide();
        }

        public void Close(Action onCompleted)
        {
            Hide(onCompleted);
        }

        public bool IsVisible => CanvasGroup.alpha > 0f && gameObject.activeSelf;

        /// <summary>Prepare or refresh data before the show transition begins.</summary>
        protected virtual void OnShow()
        {
        }

        /// <summary>Run logic after the show transition has completed.</summary>
        protected virtual void OnShown()
        {
        }

        /// <summary>Prepare for closing before the hide transition begins.</summary>
        protected virtual void OnHide()
        {
        }

        /// <summary>Run close logic after the popup has entered the hidden state.</summary>
        protected virtual void OnClose()
        {
        }

        public virtual void Initialize()
        {
            BaseLog.Log($"[UI] Initialize Popup: {gameObject.name}");
        }

        public virtual void Destroy()
        {
            BaseLog.Log($"[UI] Destroy Popup: {gameObject.name}");
            UnityEngine.Object.Destroy(gameObject);
        }

        protected virtual void OnDisable()
        {
            StopActiveTransition();
        }

        protected virtual void OnDestroy()
        {
            hideCompleted = null;
            StopActiveTransition();
        }

        private enum PopupTransitionState
        {
            Hidden,
            Showing,
            Shown,
            Hiding
        }
    }


}
