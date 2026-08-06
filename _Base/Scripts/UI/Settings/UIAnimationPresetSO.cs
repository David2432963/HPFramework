using UnityEngine;
using DG.Tweening;

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
        public float ShowDuration = 0.3f;
        public Ease ShowEase = Ease.OutQuad;
        public Vector2 ShowStartOffset = new Vector2(0f, -100f);
        public float ShowStartScale = 0.8f;

        [Header("Hide Animation")]
        public UIAnimationType HideType = UIAnimationType.Fade;
        public float HideDuration = 0.2f;
        public Ease HideEase = Ease.InQuad;
        public Vector2 HideEndOffset = new Vector2(0f, -100f);
        public float HideEndScale = 0.8f;
    }
}
