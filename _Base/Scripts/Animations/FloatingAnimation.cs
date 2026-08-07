using System.Collections;
using UnityEngine;

namespace Base.Animations
{
    /// <summary>
    /// Lightweight floating and rotation animation without external tween dependencies.
    /// </summary>
    public sealed class FloatingAnimation : MonoBehaviour
    {
        [Header("Float Settings")]
        [SerializeField] private bool enableFloat = true;
        [SerializeField] private float floatDistance = 0.5f;
        [SerializeField, Min(0.01f)] private float floatDuration = 1.5f;
        [SerializeField] private AnimationCurve floatCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Rotation Settings")]
        [SerializeField] private bool enableRotation = true;
        [SerializeField] private Vector3 rotationPerCycle = new Vector3(0f, 360f, 0f);
        [SerializeField, Min(0.01f)] private float rotationDuration = 3f;

        [Header("Timing")]
        [SerializeField] private bool useUnscaledTime = true;

        private Vector3 startPosition;
        private Quaternion startRotation;
        private Coroutine animationRoutine;
        private bool initialized;

        private void Awake()
        {
            CaptureStartState();
        }

        private void OnEnable()
        {
            CaptureStartState();
            animationRoutine = StartCoroutine(Animate());
        }

        private void OnDisable()
        {
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
            }

            if (initialized)
            {
                transform.localPosition = startPosition;
                transform.localRotation = startRotation;
            }
        }

        private void CaptureStartState()
        {
            if (initialized)
            {
                return;
            }

            startPosition = transform.localPosition;
            startRotation = transform.localRotation;
            initialized = true;
        }

        private IEnumerator Animate()
        {
            float floatTime = 0f;
            float rotationTime = 0f;
            while (true)
            {
                float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

                if (enableFloat)
                {
                    floatTime += deltaTime;
                    float phase = Mathf.Repeat(floatTime / Mathf.Max(0.01f, floatDuration), 2f);
                    float normalized = phase <= 1f ? phase : 2f - phase;
                    float evaluated = floatCurve == null
                        ? normalized
                        : floatCurve.Evaluate(normalized);
                    transform.localPosition = startPosition + Vector3.up * (evaluated * floatDistance);
                }

                if (enableRotation)
                {
                    rotationTime += deltaTime;
                    float normalized = Mathf.Repeat(
                        rotationTime / Mathf.Max(0.01f, rotationDuration),
                        1f);
                    transform.localRotation = startRotation * Quaternion.Euler(rotationPerCycle * normalized);
                }

                yield return null;
            }
        }
    }
}
