using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Base.UI.TMP
{
    public static class TMPTextExtensions
    {
        private static readonly string[] KmbUnits = { "", "K", "M", "B", "T" };

        public static async UniTask CountToAsync(
            this TMP_Text text,
            int startValue,
            int endValue,
            float duration,
            string format = "{0:N0}",
            AnimationCurve curve = null,
            CancellationToken cancellationToken = default)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (duration <= 0f)
            {
                text.text = string.Format(format, endValue);
                return;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float t = curve == null ? normalized : curve.Evaluate(normalized);
                int value = Mathf.RoundToInt(Mathf.LerpUnclamped(startValue, endValue, t));
                text.text = string.Format(format, value);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            text.text = string.Format(format, endValue);
        }

        public static async UniTask TypewriterAsync(
            this TMP_Text text,
            string fullText,
            float charactersPerSecond = 30f,
            CancellationToken cancellationToken = default)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            text.text = fullText ?? string.Empty;
            text.maxVisibleCharacters = 0;
            text.ForceMeshUpdate();
            int characterCount = text.textInfo.characterCount;
            float delay = 1f / Mathf.Max(1f, charactersPerSecond);

            for (int i = 1; i <= characterCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                text.maxVisibleCharacters = i;
                await UniTask.Delay(
                    TimeSpan.FromSeconds(delay),
                    ignoreTimeScale: true,
                    cancellationToken: cancellationToken);
            }

            text.maxVisibleCharacters = int.MaxValue;
        }

        public static void SetAlpha(this TMP_Text text, float alpha)
        {
            if (text == null) return;
            Color color = text.color;
            color.a = Mathf.Clamp01(alpha);
            text.color = color;
        }

        public static void SetOutline(this TMP_Text text, Color outlineColor, float outlineWidth)
        {
            if (text == null) return;
            if (!text.TryGetComponent(out TMPMaterialInstance materialInstance))
            {
                materialInstance = text.gameObject.AddComponent<TMPMaterialInstance>();
            }

            materialInstance.SetOutlineColor(outlineColor);
            materialInstance.SetOutlineWidth(outlineWidth);
        }

        public static string FormatAbbreviated(this long number)
        {
            if (number == 0) return "0";
            double value = Math.Abs((double)number);
            int unitIndex = 0;
            while (value >= 1000d && unitIndex < KmbUnits.Length - 1)
            {
                value /= 1000d;
                unitIndex++;
            }

            string formatted = value >= 100d
                ? value.ToString("F0")
                : value >= 10d ? value.ToString("F1") : value.ToString("F2");
            return (number < 0 ? "-" : string.Empty) + formatted + KmbUnits[unitIndex];
        }

        public static string FormatTimer(this float totalSeconds, string format = "mm\\:ss")
        {
            return TimeSpan.FromSeconds(Mathf.Max(0f, totalSeconds)).ToString(format);
        }
    }
}
