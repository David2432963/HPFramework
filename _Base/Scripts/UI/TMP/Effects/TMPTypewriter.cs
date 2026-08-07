using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using VContainer;
using Base.Audio;

namespace Base.UI.TMP
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TMPTypewriter : MonoBehaviour
    {
        private static readonly Regex PauseTagRegex = new Regex(
            @"<pause=(\d+(\.\d+)?)>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RichTextTagRegex = new Regex(
            @"<[^>]+>",
            RegexOptions.Compiled);

        [SerializeField, Min(1f)] private float charactersPerSecond = 30f;
        [SerializeField] private string typeAudioKey;
        [SerializeField, Min(0f)] private float minimumAudioInterval = 0.05f;

        private readonly Dictionary<int, float> pausesByCharacter =
            new Dictionary<int, float>();

        private TMP_Text textComponent;
        private Coroutine typewriterRoutine;
        private string parsedText = string.Empty;
        private Action completionCallback;
        private IAudioService audioService;

        public bool IsTyping => typewriterRoutine != null;

        [Inject]
        public void Construct(IAudioService audioService)
        {
            this.audioService = audioService;
        }

        private void Awake()
        {
            textComponent = GetComponent<TMP_Text>();
        }

        public void Play(string targetText, Action onComplete = null)
        {
            Stop();
            completionCallback = onComplete;
            BuildParsedText(targetText ?? string.Empty);

            textComponent.text = parsedText;
            textComponent.maxVisibleCharacters = 0;
            textComponent.ForceMeshUpdate();

            if (textComponent.textInfo.characterCount == 0 || !isActiveAndEnabled)
            {
                Complete();
                return;
            }

            typewriterRoutine = StartCoroutine(TypewriterRoutine());
        }

        public void Skip()
        {
            if (!IsTyping)
            {
                return;
            }

            StopCoroutine(typewriterRoutine);
            typewriterRoutine = null;
            Complete();
        }

        public void Stop()
        {
            if (typewriterRoutine != null)
            {
                StopCoroutine(typewriterRoutine);
                typewriterRoutine = null;
            }

            completionCallback = null;
        }

        private IEnumerator TypewriterRoutine()
        {
            int characterCount = textComponent.textInfo.characterCount;
            float characterDelay = 1f / Mathf.Max(1f, charactersPerSecond);
            float lastAudioTime = float.NegativeInfinity;

            for (int visibleCount = 1; visibleCount <= characterCount; visibleCount++)
            {
                if (pausesByCharacter.TryGetValue(visibleCount - 1, out float pauseDuration)
                    && pauseDuration > 0f)
                {
                    yield return new WaitForSecondsRealtime(pauseDuration);
                }

                textComponent.maxVisibleCharacters = visibleCount;
                if (!string.IsNullOrWhiteSpace(typeAudioKey)
                    && Time.unscaledTime - lastAudioTime >= minimumAudioInterval)
                {
                    audioService?.PlaySfx(typeAudioKey, 0.2f);
                    lastAudioTime = Time.unscaledTime;
                }

                float elapsed = 0f;
                while (elapsed < characterDelay)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            typewriterRoutine = null;
            Complete();
        }

        private void BuildParsedText(string source)
        {
            pausesByCharacter.Clear();
            int sourceIndex = 0;
            int visibleCharacterCount = 0;

            MatchCollection matches = PauseTagRegex.Matches(source);
            foreach (Match match in matches)
            {
                string segment = source.Substring(sourceIndex, match.Index - sourceIndex);
                visibleCharacterCount += CountVisibleCharacters(segment);

                if (float.TryParse(
                        match.Groups[1].Value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float duration))
                {
                    pausesByCharacter[visibleCharacterCount] = Mathf.Max(0f, duration);
                }

                sourceIndex = match.Index + match.Length;
            }

            parsedText = PauseTagRegex.Replace(source, string.Empty);
        }

        private static int CountVisibleCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            return RichTextTagRegex.Replace(text, string.Empty).Length;
        }

        private void Complete()
        {
            textComponent.text = parsedText;
            textComponent.maxVisibleCharacters = int.MaxValue;
            Action callback = completionCallback;
            completionCallback = null;
            callback?.Invoke();
        }

        private void OnDisable()
        {
            Stop();
        }

        private void OnDestroy()
        {
            Stop();
        }
    }
}
