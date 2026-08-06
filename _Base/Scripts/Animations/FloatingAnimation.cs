using UnityEngine;
using DG.Tweening;

namespace Base.Animations
{
    /// <summary>
    /// Creates a continuous floating (up/down) and rotating animation using DOTween.
    /// Can be attached to any GameObject (3D or UI).
    /// </summary>
    public class FloatingAnimation : MonoBehaviour
    {
        [Header("Float Settings")]
        [SerializeField] private bool enableFloat = true;
        [SerializeField] private float floatDistance = 0.5f;
        [SerializeField] private float floatDuration = 1.5f;
        [SerializeField] private Ease floatEase = Ease.InOutSine;

        [Header("Rotation Settings")]
        [SerializeField] private bool enableRotation = true;
        [SerializeField] private Vector3 rotationAngle = new Vector3(0, 360, 0);
        [SerializeField] private float rotationDuration = 3f;
        [SerializeField] private RotateMode rotateMode = RotateMode.FastBeyond360;
        [SerializeField] private Ease rotationEase = Ease.Linear;

        private Tween floatTween;
        private Tween rotateTween;
        private Vector3 startPosition;
        private bool isInitialized;

        private void Start()
        {
            startPosition = transform.localPosition;
            isInitialized = true;
            StartAnimations();
        }

        private void OnEnable()
        {
            if (isInitialized)
            {
                // Prevent snapping if it's already running
                StartAnimations();
            }
        }

        private void OnDisable()
        {
            KillTweens();
            if (isInitialized)
            {
                transform.localPosition = startPosition;
            }
        }

        private void OnDestroy()
        {
            KillTweens();
        }

        private void StartAnimations()
        {
            KillTweens();

            if (enableFloat)
            {
                floatTween = transform.DOLocalMoveY(startPosition.y + floatDistance, floatDuration)
                    .SetEase(floatEase)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }

            if (enableRotation)
            {
                rotateTween = transform.DOLocalRotate(rotationAngle, rotationDuration, rotateMode)
                    .SetEase(rotationEase)
                    .SetLoops(-1, LoopType.Restart)
                    .SetRelative(true)
                    .SetUpdate(true);
            }
        }

        private void KillTweens()
        {
            if (floatTween != null && floatTween.IsActive())
            {
                floatTween.Kill();
            }
            if (rotateTween != null && rotateTween.IsActive())
            {
                rotateTween.Kill();
            }
            
            floatTween = null;
            rotateTween = null;
        }
    }
}
