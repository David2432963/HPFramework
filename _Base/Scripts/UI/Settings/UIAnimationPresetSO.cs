using UnityEngine;

namespace Base.UI
{
    public enum UIAnimationType
    {
        None,
        Fade,
        Scale,
        Slide,
        FadeAndScale,
        SlideAndFade,
        ElasticScale,
        PunchScale
    }

    [CreateAssetMenu(fileName = "UIAnimationPreset", menuName = "Base/UI/Animation Preset")]
    public class UIAnimationPresetSO : ScriptableObject
    {
        [Header("Show Animation")]
        public UIAnimationType ShowType = UIAnimationType.Fade;
        [Min(0f)] public float ShowDuration = 0.3f;
        public AnimationCurve ShowCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public Vector2 ShowStartOffset = new Vector2(0f, -100f);
        [Min(0f)] public float ShowStartScale = 0.8f;

        [Header("Hide Animation")]
        public UIAnimationType HideType = UIAnimationType.Fade;
        [Min(0f)] public float HideDuration = 0.2f;
        public AnimationCurve HideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public Vector2 HideEndOffset = new Vector2(0f, -100f);
        [Min(0f)] public float HideEndScale = 0.8f;

        public float EvaluateShow(float normalizedTime)
        {
            return ShowCurve == null
                ? Mathf.Clamp01(normalizedTime)
                : ShowCurve.Evaluate(Mathf.Clamp01(normalizedTime));
        }

        public float EvaluateHide(float normalizedTime)
        {
            return HideCurve == null
                ? Mathf.Clamp01(normalizedTime)
                : HideCurve.Evaluate(Mathf.Clamp01(normalizedTime));
        }
    }
}
