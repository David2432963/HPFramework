using UnityEngine;
using DG.Tweening;
using Base.UI;
using Base;
using VContainer;

/// <summary>
/// Base class for popup UI that may be closed by back button.
/// Includes transition animation support using DOTween and CanvasGroup interactivity management.
/// Supports VContainer dependency injection for IUIService.
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
    private Tween activeTween;

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

    protected virtual void Awake()
    {
        TryGetComponent(out canvasGroup);
        TryGetComponent(out rectTransform);
        if (rectTransform != null)
        {
            originalScale = rectTransform.localScale;
            originalAnchoredPosition = rectTransform.anchoredPosition;
        }
        else
        {
            originalScale = transform.localScale;
        }
    }

    public virtual void Show()
    {
        BaseLog.Log($"[UI] Show Popup: {gameObject.name}");
        gameObject.SetActive(true);
        KillActiveTween();

        if (animationPreset == null || animationPreset.ShowType == UIAnimationType.None)
        {
            CanvasGroup.alpha = 1f;
            CanvasGroup.interactable = true;
            CanvasGroup.blocksRaycasts = true;
            if (rectTransform != null)
            {
                rectTransform.localScale = originalScale;
                rectTransform.anchoredPosition = originalAnchoredPosition;
            }
            OnShow();
            NotifyManagerShown();
            return;
        }

        CanvasGroup.interactable = false;
        CanvasGroup.blocksRaycasts = true;

        switch (animationPreset.ShowType)
        {
            case UIAnimationType.Fade:
                CanvasGroup.alpha = 0f;
                activeTween = CanvasGroup.DOFade(1f, animationPreset.ShowDuration)
                    .SetUpdate(true)
                    .SetEase(animationPreset.ShowEase)
                    .OnComplete(CompleteShow);
                break;

            case UIAnimationType.Scale:
                CanvasGroup.alpha = 1f;
                rectTransform.localScale = Vector3.zero;
                activeTween = rectTransform.DOScale(originalScale, animationPreset.ShowDuration)
                    .SetUpdate(true)
                    .SetEase(animationPreset.ShowEase)
                    .OnComplete(CompleteShow);
                break;

            case UIAnimationType.Slide:
                CanvasGroup.alpha = 1f;
                rectTransform.localScale = originalScale;
                rectTransform.anchoredPosition = originalAnchoredPosition + animationPreset.ShowStartOffset;
                activeTween = rectTransform.DOAnchorPos(originalAnchoredPosition, animationPreset.ShowDuration)
                    .SetUpdate(true)
                    .SetEase(animationPreset.ShowEase)
                    .OnComplete(CompleteShow);
                break;

            case UIAnimationType.FadeAndScale:
                CanvasGroup.alpha = 0f;
                rectTransform.localScale = originalScale * animationPreset.ShowStartScale;
                Sequence seqFadeScale = DOTween.Sequence().SetUpdate(true);
                seqFadeScale.Join(CanvasGroup.DOFade(1f, animationPreset.ShowDuration).SetEase(animationPreset.ShowEase));
                seqFadeScale.Join(rectTransform.DOScale(originalScale, animationPreset.ShowDuration).SetEase(animationPreset.ShowEase));
                seqFadeScale.OnComplete(CompleteShow);
                activeTween = seqFadeScale;
                break;

            case UIAnimationType.SlideAndFade:
                CanvasGroup.alpha = 0f;
                rectTransform.localScale = originalScale;
                rectTransform.anchoredPosition = originalAnchoredPosition + animationPreset.ShowStartOffset;
                Sequence seqSlideFade = DOTween.Sequence().SetUpdate(true);
                seqSlideFade.Join(CanvasGroup.DOFade(1f, animationPreset.ShowDuration).SetEase(animationPreset.ShowEase));
                seqSlideFade.Join(rectTransform.DOAnchorPos(originalAnchoredPosition, animationPreset.ShowDuration).SetEase(animationPreset.ShowEase));
                seqSlideFade.OnComplete(CompleteShow);
                activeTween = seqSlideFade;
                break;

            case UIAnimationType.ElasticScale:
                CanvasGroup.alpha = 1f;
                rectTransform.localScale = Vector3.zero;
                activeTween = rectTransform.DOScale(originalScale, animationPreset.ShowDuration)
                    .SetUpdate(true)
                    .SetEase(Ease.OutElastic)
                    .OnComplete(CompleteShow);
                break;

            case UIAnimationType.PunchScale:
                CanvasGroup.alpha = 1f;
                rectTransform.localScale = originalScale;
                activeTween = rectTransform.DOPunchScale(originalScale * 0.15f, animationPreset.ShowDuration, 5, 0.5f)
                    .SetUpdate(true)
                    .OnComplete(CompleteShow);
                break;
        }
    }

    public virtual void Hide()
    {
        BaseLog.Log($"[UI] Hide Popup: {gameObject.name}");
        KillActiveTween();
        OnHide();

        CanvasGroup.interactable = false;
        CanvasGroup.blocksRaycasts = false;

        if (animationPreset == null || animationPreset.HideType == UIAnimationType.None)
        {
            CanvasGroup.alpha = 0f;
            gameObject.SetActive(false);
            NotifyManagerHidden();
            return;
        }

        switch (animationPreset.HideType)
        {
            case UIAnimationType.Fade:
                activeTween = CanvasGroup.DOFade(0f, animationPreset.HideDuration)
                    .SetUpdate(true)
                    .SetEase(animationPreset.HideEase)
                    .OnComplete(CompleteHide);
                break;

            case UIAnimationType.Scale:
                activeTween = rectTransform.DOScale(Vector3.zero, animationPreset.HideDuration)
                    .SetUpdate(true)
                    .SetEase(animationPreset.HideEase)
                    .OnComplete(CompleteHide);
                break;

            case UIAnimationType.Slide:
                Vector2 targetPos = originalAnchoredPosition + animationPreset.HideEndOffset;
                activeTween = rectTransform.DOAnchorPos(targetPos, animationPreset.HideDuration)
                    .SetUpdate(true)
                    .SetEase(animationPreset.HideEase)
                    .OnComplete(CompleteHide);
                break;

            case UIAnimationType.FadeAndScale:
                Sequence seqFadeScale = DOTween.Sequence().SetUpdate(true);
                seqFadeScale.Join(CanvasGroup.DOFade(0f, animationPreset.HideDuration).SetEase(animationPreset.HideEase));
                seqFadeScale.Join(rectTransform.DOScale(originalScale * animationPreset.HideEndScale, animationPreset.HideDuration).SetEase(animationPreset.HideEase));
                seqFadeScale.OnComplete(CompleteHide);
                activeTween = seqFadeScale;
                break;

            case UIAnimationType.SlideAndFade:
                Vector2 slideTarget = originalAnchoredPosition + animationPreset.HideEndOffset;
                Sequence seqSlideFade = DOTween.Sequence().SetUpdate(true);
                seqSlideFade.Join(CanvasGroup.DOFade(0f, animationPreset.HideDuration).SetEase(animationPreset.HideEase));
                seqSlideFade.Join(rectTransform.DOAnchorPos(slideTarget, animationPreset.HideDuration).SetEase(animationPreset.HideEase));
                seqSlideFade.OnComplete(CompleteHide);
                activeTween = seqSlideFade;
                break;

            case UIAnimationType.ElasticScale:
                activeTween = rectTransform.DOScale(Vector3.zero, animationPreset.HideDuration)
                    .SetUpdate(true)
                    .SetEase(Ease.InElastic)
                    .OnComplete(CompleteHide);
                break;

            case UIAnimationType.PunchScale:
                activeTween = rectTransform.DOScale(Vector3.zero, animationPreset.HideDuration)
                    .SetUpdate(true)
                    .SetEase(Ease.InBack)
                    .OnComplete(CompleteHide);
                break;
        }
    }

    private void CompleteShow()
    {
        activeTween = null;
        CanvasGroup.alpha = 1f;
        CanvasGroup.interactable = true;
        if (rectTransform != null)
        {
            rectTransform.localScale = originalScale;
            rectTransform.anchoredPosition = originalAnchoredPosition;
        }
        OnShow();
        NotifyManagerShown();
    }

    private void CompleteHide()
    {
        activeTween = null;
        CanvasGroup.alpha = 0f;
        if (rectTransform != null)
        {
            rectTransform.localScale = originalScale;
            rectTransform.anchoredPosition = originalAnchoredPosition;
        }
        gameObject.SetActive(false);
        NotifyManagerHidden();
    }

    private void KillActiveTween()
    {
        if (activeTween != null)
        {
            activeTween.Kill();
            activeTween = null;
        }
    }

    public void Close()
    {
        Hide();
    }

    public bool IsVisible => CanvasGroup.alpha > 0f && gameObject.activeSelf;

    protected virtual void OnShow()
    {
    }

    protected virtual void OnHide()
    {
    }

    private void NotifyManagerShown()
    {
        if (uiService != null)
        {
            uiService.NotifyPopupShown(this);
        }
    }

    private void NotifyManagerHidden()
    {
        if (uiService != null)
        {
            uiService.NotifyPopupHidden(this);
        }
    }

    public virtual void Initialize()
    {
        BaseLog.Log($"[UI] Initialize Popup: {gameObject.name}");
    }

    public virtual void Destroy()
    {
        BaseLog.Log($"[UI] Destroy Popup: {gameObject.name}");
        Destroy(gameObject);
    }

    protected virtual void OnDestroy()
    {
        KillActiveTween();
    }
}
