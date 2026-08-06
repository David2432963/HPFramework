using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using VContainer;
using Base.Audio;
using Base;

namespace Base.UI
{
    /// <summary>
    /// Creates a visual "push down" scale effect when the UI element is interacted with.
    /// Attach this to any Button or interactable UI graphic.
    /// Supports VContainer dependency injection for IAudioService.
    /// </summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class UIButtonScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Header("Animation Settings")]
        [SerializeField] private float pressedScaleMultiplier = 0.9f;
        [SerializeField] private float duration = 0.1f;
        [SerializeField] private Ease easeDown = Ease.OutQuad;
        [SerializeField] private Ease easeUp = Ease.OutBack;

        private Vector3 originalScale;
        private Tween currentTween;
        private bool isPressed;
        private bool isInitialized;
        private Button attachedButton;
        private IAudioService audioService;

        [Inject]
        public void Construct(IAudioService audioService)
        {
            this.audioService = audioService;
        }

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (isInitialized) return;

            originalScale = transform.localScale;

            attachedButton = GetComponent<Button>();
            if (attachedButton != null)
            {
                attachedButton.transition = Selectable.Transition.None;
            }

            isInitialized = true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (transform is RectTransform rectTransform)
            {
                Vector2 targetPivot = new Vector2(0.5f, 0.5f);
                if (rectTransform.pivot != targetPivot)
                {
                    Vector2 pivotDelta = targetPivot - rectTransform.pivot;
                    Vector2 size = rectTransform.rect.size;
                    Vector2 offset = new Vector2(pivotDelta.x * size.x, pivotDelta.y * size.y);

                    UnityEditor.Undo.RecordObject(rectTransform, "Change Pivot Keep Position");
                    rectTransform.pivot = targetPivot;
                    rectTransform.anchoredPosition += offset;
                }
            }
        }
#endif

        private void OnEnable()
        {
            if (isInitialized)
            {
                transform.localScale = originalScale;
            }
        }

        private void OnDisable()
        {
            isPressed = false;
            KillTween();
            if (isInitialized)
            {
                transform.localScale = originalScale;
            }
        }

        private void OnDestroy()
        {
            KillTween();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Initialize();
            if (attachedButton != null && (!attachedButton.interactable || !attachedButton.enabled)) return;
            
            isPressed = true;
            if (audioService != null)
            {
                audioService.PlaySfx(BaseConstants.ButtonClickAudioKey);
            }
            KillTween();
            currentTween = transform.DOScale(originalScale * pressedScaleMultiplier, duration)
                .SetUpdate(true)
                .SetEase(easeDown);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isPressed) return;
            isPressed = false;

            KillTween();
            currentTween = transform.DOScale(originalScale, duration)
                .SetUpdate(true)
                .SetEase(easeUp);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isPressed) return;
            isPressed = false;

            KillTween();
            currentTween = transform.DOScale(originalScale, duration)
                .SetUpdate(true)
                .SetEase(easeUp);
        }

        private void KillTween()
        {
            if (currentTween != null && currentTween.IsActive())
            {
                currentTween.Kill();
            }
            currentTween = null;
        }
    }
}
