using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using VContainer;
using Base.Audio;

namespace Base.UI.TMP
{
    /// <summary>
    /// Component that animates numeric text values using DOTween with customizable easing, formatting, and optional audio key triggers.
    /// Supports VContainer dependency injection for IAudioService.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TMPNumberCounter : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string format = "{0:N0}";
        [SerializeField] private float duration = 1.0f;
        [SerializeField] private Ease ease = Ease.OutQuad;

        [Header("Audio (Optional)")]
        [SerializeField] private string stepAudioKey;

        private TMP_Text textComponent;
        private Tweener countTweener;
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

        private void OnDisable()
        {
            Stop();
        }

        private void OnDestroy()
        {
            Stop();
        }

        public void Play(int from, int to, Action onComplete = null)
        {
            Stop();

            currentValue = from;
            UpdateText(currentValue);

            int lastValue = from;

            countTweener = DOTween.To(
                () => currentValue,
                x =>
                {
                    currentValue = x;
                    if (currentValue != lastValue)
                    {
                        lastValue = currentValue;
                        PlayStepSound();
                    }
                    UpdateText(currentValue);
                },
                to,
                duration)
                .SetEase(ease)
                .SetTarget(this)
                .OnComplete(() =>
                {
                    onComplete?.Invoke();
                });
        }

        public void PlayTo(int targetValue, Action onComplete = null)
        {
            Play(currentValue, targetValue, onComplete);
        }

        public void Stop()
        {
            if (countTweener != null && countTweener.IsActive())
            {
                countTweener.Kill();
                countTweener = null;
            }
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
            if (!string.IsNullOrEmpty(stepAudioKey) && audioService != null)
            {
                audioService.PlaySfx(stepAudioKey, 0.3f);
            }
        }
    }
}
