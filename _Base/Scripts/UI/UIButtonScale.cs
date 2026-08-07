using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using VContainer;
using Base.Audio;

namespace Base.UI
{
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class UIButtonScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Header("Animation Settings")]
        [SerializeField, Range(0.1f, 1f)] private float pressedScaleMultiplier = 0.9f;
        [SerializeField, Min(0f)] private float duration = 0.1f;
        [SerializeField] private AnimationCurve pressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve releaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private Vector3 originalScale;
        private Coroutine scaleRoutine;
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
            if (isInitialized)
            {
                return;
            }

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
            if (!(transform is RectTransform rectTransform))
            {
                return;
            }

            Vector2 targetPivot = new Vector2(0.5f, 0.5f);
            if (rectTransform.pivot == targetPivot)
            {
                return;
            }

            Vector2 pivotDelta = targetPivot - rectTransform.pivot;
            Vector2 size = rectTransform.rect.size;
            Vector2 offset = new Vector2(pivotDelta.x * size.x, pivotDelta.y * size.y);
            UnityEditor.Undo.RecordObject(rectTransform, "Change Pivot Keep Position");
            rectTransform.pivot = targetPivot;
            rectTransform.anchoredPosition += offset;
        }
#endif

        private void OnEnable()
        {
            Initialize();
            transform.localScale = originalScale;
        }

        private void OnDisable()
        {
            isPressed = false;
            StopScaleRoutine();
            if (isInitialized)
            {
                transform.localScale = originalScale;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Initialize();
            if (attachedButton != null && (!attachedButton.interactable || !attachedButton.enabled))
            {
                return;
            }

            isPressed = true;
            audioService?.PlaySfx(BaseConstants.ButtonClickAudioKey);
            AnimateTo(originalScale * pressedScaleMultiplier, pressCurve);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ReleasePress();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ReleasePress();
        }

        private void ReleasePress()
        {
            if (!isPressed)
            {
                return;
            }

            isPressed = false;
            AnimateTo(originalScale, releaseCurve);
        }

        private void AnimateTo(Vector3 targetScale, AnimationCurve curve)
        {
            StopScaleRoutine();
            if (duration <= 0f || !isActiveAndEnabled)
            {
                transform.localScale = targetScale;
                return;
            }

            scaleRoutine = StartCoroutine(ScaleRoutine(targetScale, curve));
        }

        private IEnumerator ScaleRoutine(Vector3 targetScale, AnimationCurve curve)
        {
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float t = curve == null ? normalized : curve.Evaluate(normalized);
                transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
                yield return null;
            }

            transform.localScale = targetScale;
            scaleRoutine = null;
        }

        private void StopScaleRoutine()
        {
            if (scaleRoutine == null)
            {
                return;
            }

            StopCoroutine(scaleRoutine);
            scaleRoutine = null;
        }
    }
}
