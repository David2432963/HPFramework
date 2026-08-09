using System;
using System.Collections;
using TMPro;
using UnityEngine;
using VContainer;
using HP.Framework.Audio;

namespace HP.Framework.UI.TMP
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TMPNumberCounter : MonoBehaviour
    {
        [SerializeField] private string format = "{0:N0}";
        [SerializeField, Min(0f)] private float duration = 1f;
        [SerializeField] private AnimationCurve animationCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private string stepAudioKey;
        [SerializeField, Min(0f)] private float minimumAudioInterval = 0.04f;

        private TMP_Text textComponent;
        private Coroutine countRoutine;
        private int currentValue;
        private IAudioService audioService;

        [Inject]
        public void Construct(IAudioService audioService)
        {
            this.audioService = audioService;
        }

        private void Awake()
        {
            textComponent = GetComponent<TMP_Text>();
        }

        public void Play(int from, int to, Action onComplete = null)
        {
            Stop();
            currentValue = from;
            UpdateText(currentValue);

            if (duration <= 0f || !isActiveAndEnabled)
            {
                currentValue = to;
                UpdateText(currentValue);
                onComplete?.Invoke();
                return;
            }

            countRoutine = StartCoroutine(CountRoutine(from, to, onComplete));
        }

        public void PlayTo(int targetValue, Action onComplete = null)
        {
            Play(currentValue, targetValue, onComplete);
        }

        public void Stop()
        {
            if (countRoutine == null) return;
            StopCoroutine(countRoutine);
            countRoutine = null;
        }

        private IEnumerator CountRoutine(int from, int to, Action onComplete)
        {
            float elapsed = 0f;
            float lastAudioTime = float.NegativeInfinity;
            int lastValue = from;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float t = animationCurve == null
                    ? normalized
                    : animationCurve.Evaluate(normalized);
                currentValue = Mathf.RoundToInt(Mathf.LerpUnclamped(from, to, t));

                if (currentValue != lastValue)
                {
                    lastValue = currentValue;
                    UpdateText(currentValue);
                    if (Time.unscaledTime - lastAudioTime >= minimumAudioInterval)
                    {
                        PlayStepSound();
                        lastAudioTime = Time.unscaledTime;
                    }
                }

                yield return null;
            }

            if (currentValue != to)
            {
                currentValue = to;
                UpdateText(currentValue);
            }
            countRoutine = null;
            onComplete?.Invoke();
        }

        private void UpdateText(int value)
        {
            if (textComponent != null)
            {
                textComponent.text = string.Format(format, value);
            }
        }

        private void PlayStepSound()
        {
            if (!string.IsNullOrWhiteSpace(stepAudioKey))
            {
                audioService?.PlaySfx(stepAudioKey, 0.3f);
            }
        }

        private void OnDisable() => Stop();
        private void OnDestroy() => Stop();
    }
}


