using System;
using System.Text.RegularExpressions;
using DG.Tweening;
using TMPro;
using UnityEngine;
using VContainer;
using Base.Audio;

namespace Base.UI.TMP
{
    /// <summary>
    /// DOTween-powered Typewriter component with support for rich-text tags, custom speech pause tags (&lt;pause=0.5&gt;), audio SFX, and instant Skip.
    /// Supports VContainer dependency injection for IAudioService.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TMPTypewriter : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float charactersPerSecond = 30f;
        [SerializeField] private string typeAudioKey;

        private static readonly Regex PauseTagRegex = new Regex(@"<pause=(\d+(\.\d+)?)>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private TMP_Text textComponent;
        private Sequence typewriterSequence;
        private string fullParsedText;
        private Action onTypewriterComplete;
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

        public void Play(string targetText, Action onComplete = null)
        {
            Stop();
            onTypewriterComplete = onComplete;

            if (string.IsNullOrEmpty(targetText))
            {
                if (textComponent != null) textComponent.text = string.Empty;
                onTypewriterComplete?.Invoke();
                return;
            }

            fullParsedText = targetText;
            typewriterSequence = DOTween.Sequence().SetTarget(this);

            MatchCollection matches = PauseTagRegex.Matches(targetText);
            int lastIndex = 0;
            string cleanAccumulatedText = string.Empty;

            foreach (Match match in matches)
            {
                int matchIndex = match.Index;
                if (matchIndex > lastIndex)
                {
                    string textSegment = targetText.Substring(lastIndex, matchIndex - lastIndex);
                    AppendTextSegmentToSequence(textSegment, ref cleanAccumulatedText);
                }

                if (float.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float pauseDuration))
                {
                    typewriterSequence.AppendInterval(pauseDuration);
                }

                lastIndex = matchIndex + match.Length;
            }

            if (lastIndex < targetText.Length)
            {
                string remainingText = targetText.Substring(lastIndex);
                AppendTextSegmentToSequence(remainingText, ref cleanAccumulatedText);
            }

            typewriterSequence.OnComplete(() =>
            {
                if (textComponent != null) textComponent.text = RemovePauseTags(fullParsedText);
                Action callback = onTypewriterComplete;
                onTypewriterComplete = null;
                callback?.Invoke();
            });
        }

        public void Skip()
        {
            if (IsTyping)
            {
                typewriterSequence.Complete(true);
            }
        }

        public void Stop()
        {
            if (typewriterSequence != null && typewriterSequence.IsActive())
            {
                typewriterSequence.Kill();
                typewriterSequence = null;
            }
        }

        public bool IsTyping => typewriterSequence != null && typewriterSequence.IsActive() && typewriterSequence.IsPlaying();

        private void AppendTextSegmentToSequence(string segment, ref string accumulatedText)
        {
            string cleanSegment = RemovePauseTags(segment);
            int segmentLength = cleanSegment.Length;
            if (segmentLength <= 0) return;

            float duration = segmentLength / Mathf.Max(1f, charactersPerSecond);
            string previousText = accumulatedText;
            accumulatedText += cleanSegment;

            int currentVisibleCount = 0;
            typewriterSequence.Append(DOTween.To(
                () => currentVisibleCount,
                x =>
                {
                    currentVisibleCount = x;
                    if (textComponent != null)
                    {
                        textComponent.text = previousText + cleanSegment.Substring(0, Mathf.Min(x, cleanSegment.Length));
                    }
                    PlayTypeSound();
                },
                segmentLength,
                duration).SetEase(Ease.Linear));
        }

        private string RemovePauseTags(string input)
        {
            return PauseTagRegex.Replace(input, string.Empty);
        }

        private void PlayTypeSound()
        {
            if (!string.IsNullOrEmpty(typeAudioKey) && audioService != null)
            {
                audioService.PlaySfx(typeAudioKey, 0.2f);
            }
        }
    }
}
