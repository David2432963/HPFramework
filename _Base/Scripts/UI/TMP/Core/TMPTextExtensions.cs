using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Base.UI.TMP
{
    /// <summary>
    /// Extension methods for TMP_Text providing DOTween animation helpers, formatting utilities, and styling.
    /// </summary>
    public static class TMPTextExtensions
    {
        private static readonly string[] KMBUnits = { "", "K", "M", "B", "T" };

        /// <summary>
        /// Animates an integer number count from start to end using DOTween.
        /// </summary>
        public static Tweener DOCount(
            this TMP_Text text,
            int startValue,
            int endValue,
            float duration,
            string format = "{0:N0}",
            Ease ease = Ease.OutQuad)
        {
            if (text == null)
            {
                return null;
            }

            int currentValue = startValue;
            text.text = string.Format(format, currentValue);

            return DOTween.To(
                () => currentValue,
                x =>
                {
                    currentValue = x;
                    text.text = string.Format(format, currentValue);
                },
                endValue,
                duration)
                .SetEase(ease)
                .SetTarget(text);
        }

        /// <summary>
        /// Animates text typing character-by-character using DOTween.
        /// </summary>
        public static Tweener DOTypewriter(
            this TMP_Text text,
            string fullText,
            float duration,
            Ease ease = Ease.Linear)
        {
            if (text == null || fullText == null)
            {
                return null;
            }

            text.text = string.Empty;
            int totalLength = fullText.Length;
            int currentLength = 0;

            return DOTween.To(
                () => currentLength,
                x =>
                {
                    currentLength = x;
                    text.text = fullText.Substring(0, Mathf.Min(currentLength, totalLength));
                },
                totalLength,
                duration)
                .SetEase(ease)
                .SetTarget(text);
        }

        /// <summary>
        /// Sets the alpha value of the text component cleanly.
        /// </summary>
        public static void SetAlpha(this TMP_Text text, float alpha)
        {
            if (text != null)
            {
                Color color = text.color;
                color.a = Mathf.Clamp01(alpha);
                text.color = color;
            }
        }

        /// <summary>
        /// Sets the outline color and width on the TMPMaterialInstance safely without leaking materials.
        /// </summary>
        public static void SetOutline(this TMP_Text text, Color outlineColor, float outlineWidth)
        {
            if (text == null)
            {
                return;
            }

            if (!text.TryGetComponent(out TMPMaterialInstance matInstance))
            {
                matInstance = text.gameObject.AddComponent<TMPMaterialInstance>();
            }

            matInstance.SetOutlineColor(outlineColor);
            matInstance.SetOutlineWidth(outlineWidth);
        }

        /// <summary>
        /// Converts large numbers to compact human-readable string formats (e.g., 1500 -> 1.5K, 2500000 -> 2.5M).
        /// </summary>
        public static string FormatAbbreviated(this long number)
        {
            if (number == 0)
            {
                return "0";
            }

            double num = Math.Abs(number);
            int unitIndex = 0;

            while (num >= 1000 && unitIndex < KMBUnits.Length - 1)
            {
                num /= 1000.0;
                unitIndex++;
            }

            string result = num >= 100 ? num.ToString("F0") : (num >= 10 ? num.ToString("F1") : num.ToString("F2"));
            string sign = number < 0 ? "-" : "";

            return $"{sign}{result}{KMBUnits[unitIndex]}";
        }

        /// <summary>
        /// Formats seconds into a standard timer string (e.g. 125s -> "02:05").
        /// </summary>
        public static string FormatTimer(this float totalSeconds, string format = "mm\\:ss")
        {
            TimeSpan time = TimeSpan.FromSeconds(Mathf.Max(0, totalSeconds));
            return time.ToString(format);
        }
    }
}
