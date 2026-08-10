using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using HP.Framework.Pooling;

namespace HP.Framework.UI
{
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
        private object animationTween;
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
            if (TryPlayDotweenAnimation())
            {
                return;
            }

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
            if (animationTween != null)
            {
                DotweenBridge.TryKill(animationTween);
                animationTween = null;
            }

            if (animationRoutine == null)
            {
                return;
            }

            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        private bool TryPlayDotweenAnimation()
        {
            if (!DotweenBridge.IsAvailable || canvasGroup == null || fadeDuration <= 0f)
            {
                return false;
            }

            Vector2 startPosition = restingPosition - Vector2.up * slideOffset;
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = startPosition;
            }

            canvasGroup.alpha = 0f;

            if (!DotweenBridge.TryCreateSequence(out object sequence))
            {
                return false;
            }

            bool hasTween = false;
            AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            if (rectTransform != null)
            {
                if (!DotweenBridge.TryCreateAnchorPosTween(
                    rectTransform,
                    restingPosition,
                    fadeDuration,
                    out object moveInTween))
                {
                    DotweenBridge.TryKill(sequence);
                    return false;
                }

                if (!DotweenBridge.TrySetEase(moveInTween, easeCurve))
                {
                    DotweenBridge.TryKill(sequence);
                    return false;
                }

                if (!DotweenBridge.TryInsert(sequence, 0f, moveInTween))
                {
                    DotweenBridge.TryKill(sequence);
                    return false;
                }

                hasTween = true;
            }

            if (!DotweenBridge.TryCreateFadeTween(canvasGroup, 1f, fadeDuration, out object fadeInTween))
            {
                DotweenBridge.TryKill(sequence);
                return false;
            }

            if (!DotweenBridge.TrySetEase(fadeInTween, easeCurve))
            {
                DotweenBridge.TryKill(sequence);
                return false;
            }

            if (!DotweenBridge.TryInsert(sequence, 0f, fadeInTween))
            {
                DotweenBridge.TryKill(sequence);
                return false;
            }

            hasTween = true;

            if (showDuration > 0f)
            {
                if (!DotweenBridge.TryAppendInterval(sequence, showDuration))
                {
                    DotweenBridge.TryKill(sequence);
                    return false;
                }
            }

            if (!DotweenBridge.TryCreateFadeTween(canvasGroup, 0f, fadeDuration, out object fadeOutTween))
            {
                DotweenBridge.TryKill(sequence);
                return false;
            }

            if (!DotweenBridge.TrySetEase(fadeOutTween, easeCurve))
            {
                DotweenBridge.TryKill(sequence);
                return false;
            }

            if (!DotweenBridge.TryAppend(sequence, fadeOutTween))
            {
                DotweenBridge.TryKill(sequence);
                return false;
            }

            hasTween = true;

            if (!hasTween)
            {
                DotweenBridge.TryKill(sequence);
                return false;
            }

            Action finish = () =>
            {
                animationTween = null;
                if (poolService != null)
                {
                    poolService.Release(this);
                }
                else
                {
                    Destroy(gameObject);
                }
            };

            if (!DotweenBridge.TryOnComplete(sequence, finish))
            {
                DotweenBridge.TryKill(sequence);
                return false;
            }

            if (!DotweenBridge.TrySetUpdateIndependent(sequence))
            {
                DotweenBridge.TryKill(sequence);
                return false;
            }

            animationTween = sequence;
            return true;
        }

        private void OnDisable()
        {
            StopAnimation();
        }
    }
}
