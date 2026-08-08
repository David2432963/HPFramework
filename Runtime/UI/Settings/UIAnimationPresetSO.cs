using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace HP.Framework.UI
{
    // Numeric values intentionally mirror DOTween's Ease enum so presets migrated
    // from projects that used DOTween keep their serialized ease values intact.
    public enum UIEase
    {
        Unset = 0,
        Linear = 1,
        InSine = 2,
        OutSine = 3,
        InOutSine = 4,
        InQuad = 5,
        OutQuad = 6,
        InOutQuad = 7,
        InCubic = 8,
        OutCubic = 9,
        InOutCubic = 10,
        InQuart = 11,
        OutQuart = 12,
        InOutQuart = 13,
        InQuint = 14,
        OutQuint = 15,
        InOutQuint = 16,
        InExpo = 17,
        OutExpo = 18,
        InOutExpo = 19,
        InCirc = 20,
        OutCirc = 21,
        InOutCirc = 22,
        InElastic = 23,
        OutElastic = 24,
        InOutElastic = 25,
        InBack = 26,
        OutBack = 27,
        InOutBack = 28,
        InBounce = 29,
        OutBounce = 30,
        InOutBounce = 31
    }

    [CreateAssetMenu(
        fileName = "UIAnimationPreset",
        menuName = "HP Framework/UI/Animation Preset")]
    public sealed class UIAnimationPresetSO : ScriptableObject
    {
        [Serializable]
        public sealed class Timing
        {
            [Min(0f)] public float Delay;
            [Min(0f)] public float Duration = 0.2f;
            public bool UseCustomCurve;
            public UIEase Ease = UIEase.OutQuad;
            public AnimationCurve Curve =
                AnimationCurve.Linear(0f, 0f, 1f, 1f);

            public float TotalDuration =>
                Mathf.Max(0f, Delay) + Mathf.Max(0f, Duration);
        }

        [Serializable]
        public sealed class FadeTransition
        {
            public bool Enabled = true;
            public Timing Timing = new Timing();
        }

        [Serializable]
        public sealed class ScaleTransition
        {
            public bool Enabled = true;
            [Min(0.01f)] public float Scale = 0.75f;
            public Timing Timing = new Timing();
        }

        [Serializable]
        public sealed class TransitionPhase
        {
            public FadeTransition BackdropFade = new FadeTransition();
            public ScaleTransition ContentScale = new ScaleTransition();
            public FadeTransition ContentFade = new FadeTransition();

            public float TotalDuration => Mathf.Max(
                BackdropFade.Enabled
                    ? BackdropFade.Timing.TotalDuration
                    : 0f,
                Mathf.Max(
                    ContentScale.Enabled
                        ? ContentScale.Timing.TotalDuration
                        : 0f,
                    ContentFade.Enabled
                        ? ContentFade.Timing.TotalDuration
                        : 0f));
        }

        [FormerlySerializedAs("PopupShow")]
        public TransitionPhase Show = CreateShowDefaults();

        [FormerlySerializedAs("PopupHide")]
        public TransitionPhase Hide = CreateHideDefaults();

        public bool UsesBackdropFade =>
            Show.BackdropFade.Enabled || Hide.BackdropFade.Enabled;

        public bool UsesContentScale =>
            Show.ContentScale.Enabled || Hide.ContentScale.Enabled;

        public bool UsesContentFade =>
            Show.ContentFade.Enabled || Hide.ContentFade.Enabled;

        private static TransitionPhase CreateShowDefaults()
        {
            return new TransitionPhase
            {
                BackdropFade = new FadeTransition
                {
                    Timing = new Timing
                    {
                        Duration = 0.18f,
                        Ease = UIEase.OutQuad
                    }
                },
                ContentScale = new ScaleTransition
                {
                    Scale = 0.72f,
                    Timing = new Timing
                    {
                        Delay = 0.02f,
                        Duration = 0.28f,
                        Ease = UIEase.OutBack
                    }
                },
                ContentFade = new FadeTransition
                {
                    Enabled = false,
                    Timing = new Timing
                    {
                        Duration = 0.14f,
                        Ease = UIEase.OutQuad
                    }
                }
            };
        }

        private static TransitionPhase CreateHideDefaults()
        {
            return new TransitionPhase
            {
                BackdropFade = new FadeTransition
                {
                    Timing = new Timing
                    {
                        Delay = 0.02f,
                        Duration = 0.2f,
                        Ease = UIEase.InQuad
                    }
                },
                ContentScale = new ScaleTransition
                {
                    Scale = 0.86f,
                    Timing = new Timing
                    {
                        Duration = 0.16f,
                        Ease = UIEase.InBack
                    }
                },
                ContentFade = new FadeTransition
                {
                    Enabled = false,
                    Timing = new Timing
                    {
                        Duration = 0.12f,
                        Ease = UIEase.InQuad
                    }
                }
            };
        }

        public static float EvaluateTiming(Timing timing, float phaseTime)
        {
            if (timing == null)
            {
                return 1f;
            }

            if (phaseTime < timing.Delay)
            {
                return 0f;
            }

            if (timing.Duration <= 0f)
            {
                return 1f;
            }

            float normalizedTime = Mathf.Clamp01(
                (phaseTime - timing.Delay) / timing.Duration);
            return timing.UseCustomCurve && timing.Curve != null
                ? timing.Curve.Evaluate(normalizedTime)
                : EvaluateEase(timing.Ease, normalizedTime);
        }

        public static float EvaluateEase(UIEase ease, float time)
        {
            float t = Mathf.Clamp01(time);
            switch (ease)
            {
                case UIEase.InSine:
                    return 1f - Mathf.Cos(t * Mathf.PI * 0.5f);
                case UIEase.OutSine:
                    return Mathf.Sin(t * Mathf.PI * 0.5f);
                case UIEase.InOutSine:
                    return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
                case UIEase.InQuad:
                    return t * t;
                case UIEase.OutQuad:
                    return 1f - (1f - t) * (1f - t);
                case UIEase.InOutQuad:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
                case UIEase.InCubic:
                    return t * t * t;
                case UIEase.OutCubic:
                    return 1f - Mathf.Pow(1f - t, 3f);
                case UIEase.InOutCubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
                case UIEase.InQuart:
                    return t * t * t * t;
                case UIEase.OutQuart:
                    return 1f - Mathf.Pow(1f - t, 4f);
                case UIEase.InOutQuart:
                    return t < 0.5f ? 8f * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 4f) * 0.5f;
                case UIEase.InQuint:
                    return t * t * t * t * t;
                case UIEase.OutQuint:
                    return 1f - Mathf.Pow(1f - t, 5f);
                case UIEase.InOutQuint:
                    return t < 0.5f ? 16f * t * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 5f) * 0.5f;
                case UIEase.InExpo:
                    return Mathf.Approximately(t, 0f) ? 0f : Mathf.Pow(2f, 10f * t - 10f);
                case UIEase.OutExpo:
                    return Mathf.Approximately(t, 1f) ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                case UIEase.InOutExpo:
                    if (Mathf.Approximately(t, 0f) || Mathf.Approximately(t, 1f)) return t;
                    return t < 0.5f
                        ? Mathf.Pow(2f, 20f * t - 10f) * 0.5f
                        : (2f - Mathf.Pow(2f, -20f * t + 10f)) * 0.5f;
                case UIEase.InCirc:
                    return 1f - Mathf.Sqrt(1f - t * t);
                case UIEase.OutCirc:
                    return Mathf.Sqrt(1f - Mathf.Pow(t - 1f, 2f));
                case UIEase.InOutCirc:
                    return t < 0.5f
                        ? (1f - Mathf.Sqrt(1f - Mathf.Pow(2f * t, 2f))) * 0.5f
                        : (Mathf.Sqrt(1f - Mathf.Pow(-2f * t + 2f, 2f)) + 1f) * 0.5f;
                case UIEase.InBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    return c3 * t * t * t - c1 * t * t;
                }
                case UIEase.OutBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    float x = t - 1f;
                    return 1f + c3 * x * x * x + c1 * x * x;
                }
                case UIEase.InOutBack:
                {
                    const float c1 = 1.70158f;
                    const float c2 = c1 * 1.525f;
                    return t < 0.5f
                        ? Mathf.Pow(2f * t, 2f) * ((c2 + 1f) * 2f * t - c2) * 0.5f
                        : (Mathf.Pow(2f * t - 2f, 2f) * ((c2 + 1f) * (t * 2f - 2f) + c2) + 2f) * 0.5f;
                }
                case UIEase.InElastic:
                    return 1f - EvaluateEase(UIEase.OutElastic, 1f - t);
                case UIEase.OutElastic:
                    if (Mathf.Approximately(t, 0f) || Mathf.Approximately(t, 1f)) return t;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
                case UIEase.InOutElastic:
                    if (Mathf.Approximately(t, 0f) || Mathf.Approximately(t, 1f)) return t;
                    return t < 0.5f
                        ? -(Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * (2f * Mathf.PI / 4.5f))) * 0.5f
                        : Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * (2f * Mathf.PI / 4.5f)) * 0.5f + 1f;
                case UIEase.InBounce:
                    return 1f - EvaluateEase(UIEase.OutBounce, 1f - t);
                case UIEase.OutBounce:
                    return EvaluateOutBounce(t);
                case UIEase.InOutBounce:
                    return t < 0.5f
                        ? (1f - EvaluateOutBounce(1f - 2f * t)) * 0.5f
                        : (1f + EvaluateOutBounce(2f * t - 1f)) * 0.5f;
                case UIEase.Unset:
                case UIEase.Linear:
                default:
                    return t;
            }
        }

        private static float EvaluateOutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1)
            {
                t -= 1.5f / d1;
                return n1 * t * t + 0.75f;
            }
            if (t < 2.5f / d1)
            {
                t -= 2.25f / d1;
                return n1 * t * t + 0.9375f;
            }

            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }

        private void OnEnable()
        {
            EnsureValidData();
        }

        private void OnValidate()
        {
            EnsureValidData();
        }

        private void EnsureValidData()
        {
            Show ??= CreateShowDefaults();
            Hide ??= CreateHideDefaults();

            ValidatePhase(Show);
            ValidatePhase(Hide);
        }

        private static void ValidatePhase(TransitionPhase phase)
        {
            phase.BackdropFade ??= new FadeTransition();
            phase.ContentScale ??= new ScaleTransition();
            phase.ContentFade ??= new FadeTransition();

            EnsureTiming(phase.BackdropFade);
            EnsureTiming(phase.ContentScale);
            EnsureTiming(phase.ContentFade);
            phase.ContentScale.Scale =
                Mathf.Max(0.01f, phase.ContentScale.Scale);
        }

        private static void EnsureTiming(FadeTransition transition)
        {
            transition.Timing ??= new Timing();
            ValidateTiming(transition.Timing);
        }

        private static void EnsureTiming(ScaleTransition transition)
        {
            transition.Timing ??= new Timing();
            ValidateTiming(transition.Timing);
        }

        private static void ValidateTiming(Timing timing)
        {
            timing.Delay = Mathf.Max(0f, timing.Delay);
            timing.Duration = Mathf.Max(0f, timing.Duration);
            if (timing.Curve == null || timing.Curve.length < 2)
            {
                timing.Curve =
                    AnimationCurve.Linear(0f, 0f, 1f, 1f);
            }
        }
    }
}


