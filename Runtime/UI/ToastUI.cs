namespace HP.Framework.UI
{
    using System.Collections;
    using UnityEngine;
    using UnityEngine.UI;
    using VContainer;
    using HP.Framework.Pooling;

    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ToastUI : MonoBehaviour, IPoolable
    {
        [SerializeField] private Text messageText;
        [SerializeField, Min(0f)] private float showDuration = 1.5f;
        [SerializeField, Min(0f)] private float fadeDuration = 0.3f;
        [SerializeField] private float slideOffset = 50f;

        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private Vector2 restingPosition;
        private Coroutine animationRoutine;
        private IPoolService poolService;

        [Inject]
        public void Construct(IPoolService poolService)
        {
            this.poolService = poolService;
        }

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = transform as RectTransform;
            if (rectTransform != null)
            {
                restingPosition = rectTransform.anchoredPosition;
            }

            canvasGroup.alpha = 0f;
        }

        public void Show(string message)
        {
            if (messageText != null)
            {
                messageText.text = message ?? string.Empty;
            }

            StopAnimation();
            gameObject.SetActive(true);
            animationRoutine = StartCoroutine(AnimateToast());
        }

        public void OnSpawn()
        {
            StopAnimation();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = restingPosition;
            }
        }

        public void OnDespawn()
        {
            StopAnimation();
            if (messageText != null)
            {
                messageText.text = string.Empty;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = restingPosition;
            }
        }

        private IEnumerator AnimateToast()
        {
            Vector2 startPosition = restingPosition - Vector2.up * slideOffset;
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = startPosition;
            }

            yield return AnimatePhase(0f, 1f, startPosition, restingPosition, fadeDuration);

            if (showDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(showDuration);
            }

            yield return AnimatePhase(1f, 0f, restingPosition, restingPosition, fadeDuration);
            animationRoutine = null;

            if (poolService != null)
            {
                poolService.Release(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private IEnumerator AnimatePhase(
            float fromAlpha,
            float toAlpha,
            Vector2 fromPosition,
            Vector2 toPosition,
            float duration)
        {
            if (duration <= 0f)
            {
                canvasGroup.alpha = toAlpha;
                if (rectTransform != null)
                {
                    rectTransform.anchoredPosition = toPosition;
                }
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
                if (rectTransform != null)
                {
                    rectTransform.anchoredPosition = Vector2.Lerp(fromPosition, toPosition, t);
                }
                yield return null;
            }

            canvasGroup.alpha = toAlpha;
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = toPosition;
            }
        }

        private void StopAnimation()
        {
            if (animationRoutine == null)
            {
                return;
            }

            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        private void OnDisable()
        {
            StopAnimation();
        }
    }


}


