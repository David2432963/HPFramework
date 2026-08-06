using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using VContainer;
using Base.Pooling;

[RequireComponent(typeof(CanvasGroup))]
public sealed class ToastUI : MonoBehaviour
{
    [SerializeField] private Text messageText;
    [SerializeField] private float showDuration = 1.5f;
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private float slideOffset = 50f;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Tween activeTween;
    private IPoolService poolService;

    [Inject]
    public void Construct(IPoolService poolService)
    {
        this.poolService = poolService;
    }

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();

        canvasGroup.alpha = 0f;
    }

    public void Show(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }

        if (activeTween != null)
        {
            activeTween.Kill();
        }

        canvasGroup.alpha = 0f;
        Vector2 startPos = rectTransform.anchoredPosition;

        rectTransform.anchoredPosition = new Vector2(startPos.x, startPos.y - slideOffset);

        Sequence seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(canvasGroup.DOFade(1f, fadeDuration))
           .Join(rectTransform.DOAnchorPosY(startPos.y, fadeDuration))
           .AppendInterval(showDuration)
           .Append(canvasGroup.DOFade(0f, fadeDuration))
           .OnComplete(() =>
           {
               if (poolService != null)
               {
                   poolService.Release(this);
               }
               else
               {
                   Destroy(gameObject);
               }
           });

        activeTween = seq;
        gameObject.SetActive(true);
    }

    private void OnDestroy()
    {
        if (activeTween != null)
        {
            activeTween.Kill();
        }
    }
}
