using System.Collections;
using UnityEngine;
using VContainer;
using Base.UI;

/// <summary>
/// Base popup with coroutine-driven unscaled-time transitions.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class BasePopup : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] protected UIAnimationPresetSO animationPreset;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector3 originalScale;
    private Vector2 originalAnchoredPosition;
    private Coroutine activeTransition;

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
                canvasGroup = GetComponent<CanvasGroup>();
            }

            return canvasGroup;
        }
    }

    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = transform as RectTransform;
        originalScale = transform.localScale;
        if (rectTransform != null)
        {
            originalAnchoredPosition = rectTransform.anchoredPosition;
        }
    }

    public virtual void Show()
    {
        gameObject.SetActive(true);
        StopActiveTransition();
        ResetTransformState();

        CanvasGroup.interactable = false;
        CanvasGroup.blocksRaycasts = true;

        if (animationPreset == null
            || animationPreset.ShowType == UIAnimationType.None
            || animationPreset.ShowDuration <= 0f)
        {
            CompleteShow();
            return;
        }

        activeTransition = StartCoroutine(PlayShowTransition());
    }

    public virtual void Hide()
    {
        StopActiveTransition();
        OnHide();
        CanvasGroup.interactable = false;
        CanvasGroup.blocksRaycasts = false;

        if (animationPreset == null
            || animationPreset.HideType == UIAnimationType.None
            || animationPreset.HideDuration <= 0f)
        {
            CompleteHide();
            return;
        }

        activeTransition = StartCoroutine(PlayHideTransition());
    }

    private IEnumerator PlayShowTransition()
    {
        UIAnimationType type = animationPreset.ShowType;
        float duration = Mathf.Max(0.001f, animationPreset.ShowDuration);
        Vector3 startScale = originalScale;
        Vector2 startPosition = originalAnchoredPosition;
        float startAlpha = 1f;

        switch (type)
        {
            case UIAnimationType.Fade:
                startAlpha = 0f;
                break;
            case UIAnimationType.Scale:
            case UIAnimationType.ElasticScale:
                startScale = Vector3.zero;
                break;
            case UIAnimationType.PunchScale:
                startScale = originalScale * 0.85f;
                break;
            case UIAnimationType.Slide:
                startPosition = originalAnchoredPosition + animationPreset.ShowStartOffset;
                break;
            case UIAnimationType.FadeAndScale:
                startAlpha = 0f;
                startScale = originalScale * animationPreset.ShowStartScale;
                break;
            case UIAnimationType.SlideAndFade:
                startAlpha = 0f;
                startPosition = originalAnchoredPosition + animationPreset.ShowStartOffset;
                break;
        }

        CanvasGroup.alpha = startAlpha;
        transform.localScale = startScale;
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = startPosition;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float t = animationPreset.EvaluateShow(normalized);
            if (type == UIAnimationType.PunchScale)
            {
                t = Mathf.Clamp01(t);
                float punch = Mathf.Sin(t * Mathf.PI) * (1f - t) * 0.12f;
                transform.localScale = Vector3.LerpUnclamped(
                    startScale,
                    originalScale,
                    t) + originalScale * punch;
            }
            else
            {
                transform.localScale = Vector3.LerpUnclamped(startScale, originalScale, t);
            }

            CanvasGroup.alpha = Mathf.LerpUnclamped(startAlpha, 1f, t);
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(
                    startPosition,
                    originalAnchoredPosition,
                    t);
            }

            yield return null;
        }

        CompleteShow();
    }

    private IEnumerator PlayHideTransition()
    {
        UIAnimationType type = animationPreset.HideType;
        float duration = Mathf.Max(0.001f, animationPreset.HideDuration);
        Vector3 startScale = transform.localScale;
        Vector3 endScale = originalScale;
        Vector2 startPosition = rectTransform != null
            ? rectTransform.anchoredPosition
            : originalAnchoredPosition;
        Vector2 endPosition = originalAnchoredPosition;
        float startAlpha = CanvasGroup.alpha;
        float endAlpha = startAlpha;

        switch (type)
        {
            case UIAnimationType.Fade:
                endAlpha = 0f;
                break;
            case UIAnimationType.Scale:
            case UIAnimationType.ElasticScale:
            case UIAnimationType.PunchScale:
                endScale = Vector3.zero;
                break;
            case UIAnimationType.Slide:
                endPosition = originalAnchoredPosition + animationPreset.HideEndOffset;
                break;
            case UIAnimationType.FadeAndScale:
                endAlpha = 0f;
                endScale = originalScale * animationPreset.HideEndScale;
                break;
            case UIAnimationType.SlideAndFade:
                endAlpha = 0f;
                endPosition = originalAnchoredPosition + animationPreset.HideEndOffset;
                break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = animationPreset.EvaluateHide(elapsed / duration);
            transform.localScale = Vector3.LerpUnclamped(startScale, endScale, t);
            CanvasGroup.alpha = Mathf.LerpUnclamped(startAlpha, endAlpha, t);
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(
                    startPosition,
                    endPosition,
                    t);
            }

            yield return null;
        }

        CompleteHide();
    }

    private void CompleteShow()
    {
        activeTransition = null;
        ResetTransformState();
        CanvasGroup.alpha = 1f;
        CanvasGroup.interactable = true;
        CanvasGroup.blocksRaycasts = true;
        OnShow();
        uiService?.NotifyPopupShown(this);
    }

    private void CompleteHide()
    {
        activeTransition = null;
        CanvasGroup.alpha = 0f;
        ResetTransformState();
        gameObject.SetActive(false);
        uiService?.NotifyPopupHidden(this);
    }

    private void ResetTransformState()
    {
        transform.localScale = originalScale;
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = originalAnchoredPosition;
        }
    }

    private void StopActiveTransition()
    {
        if (activeTransition == null)
        {
            return;
        }

        StopCoroutine(activeTransition);
        activeTransition = null;
    }

    public void Close()
    {
        Hide();
    }

    public bool IsVisible => gameObject.activeSelf && CanvasGroup.alpha > 0f;

    protected virtual void OnShow()
    {
    }

    protected virtual void OnHide()
    {
    }

    public virtual void Initialize()
    {
    }

    public virtual void Destroy()
    {
        UnityEngine.Object.Destroy(gameObject);
    }

    protected virtual void OnDisable()
    {
        StopActiveTransition();
    }

    protected virtual void OnDestroy()
    {
        StopActiveTransition();
    }
}
