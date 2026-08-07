using UnityEngine;
using UnityEditor;
using Base.UI;

namespace Base.Editor.UI
{
    public static class UIAnimationPresetGenerator
    {
        [MenuItem("Base/Generate UI Animation Presets", false, 30)]
        public static void GeneratePresets()
        {
            const string folderPath = "Assets/_Base/Data/UI/Animations";
            EnsureFolder(folderPath);

            CreatePreset(
                folderPath,
                "Anim_FadeAndScale",
                UIAnimationType.FadeAndScale,
                0.3f,
                EaseOutBack(),
                Vector2.zero,
                0.7f,
                UIAnimationType.FadeAndScale,
                0.2f,
                EaseInBack(),
                Vector2.zero,
                0.7f);

            CreatePreset(
                folderPath,
                "Anim_SlideUpFade",
                UIAnimationType.SlideAndFade,
                0.4f,
                EaseOutCubic(),
                new Vector2(0f, -200f),
                1f,
                UIAnimationType.SlideAndFade,
                0.25f,
                EaseInCubic(),
                new Vector2(0f, -200f),
                1f);

            CreatePreset(
                folderPath,
                "Anim_ElasticPop",
                UIAnimationType.ElasticScale,
                0.6f,
                ElasticOut(),
                Vector2.zero,
                0f,
                UIAnimationType.Scale,
                0.2f,
                EaseInBack(),
                Vector2.zero,
                0f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UI] Generated animation presets in {folderPath}");
        }

        private static void CreatePreset(
            string folder,
            string name,
            UIAnimationType showType,
            float showDuration,
            AnimationCurve showCurve,
            Vector2 showOffset,
            float showScale,
            UIAnimationType hideType,
            float hideDuration,
            AnimationCurve hideCurve,
            Vector2 hideOffset,
            float hideScale)
        {
            string path = $"{folder}/{name}.asset";
            UIAnimationPresetSO preset = AssetDatabase.LoadAssetAtPath<UIAnimationPresetSO>(path);
            bool isNew = preset == null;
            if (isNew)
            {
                preset = ScriptableObject.CreateInstance<UIAnimationPresetSO>();
            }

            preset.ShowType = showType;
            preset.ShowDuration = showDuration;
            preset.ShowCurve = showCurve;
            preset.ShowStartOffset = showOffset;
            preset.ShowStartScale = showScale;
            preset.HideType = hideType;
            preset.HideDuration = hideDuration;
            preset.HideCurve = hideCurve;
            preset.HideEndOffset = hideOffset;
            preset.HideEndScale = hideScale;

            if (isNew)
            {
                AssetDatabase.CreateAsset(preset, path);
            }
            else
            {
                EditorUtility.SetDirty(preset);
            }
        }

        private static AnimationCurve EaseOutCubic()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 3f),
                new Keyframe(1f, 1f, 0f, 0f));
        }

        private static AnimationCurve EaseInCubic()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(1f, 1f, 3f, 0f));
        }

        private static AnimationCurve EaseOutBack()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 2.5f),
                new Keyframe(0.75f, 1.08f, 0f, 0f),
                new Keyframe(1f, 1f, -0.4f, 0f));
        }

        private static AnimationCurve EaseInBack()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, -0.4f),
                new Keyframe(0.25f, -0.08f, 0f, 0f),
                new Keyframe(1f, 1f, 2.5f, 0f));
        }

        private static AnimationCurve ElasticOut()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.55f, 1.15f),
                new Keyframe(0.75f, 0.95f),
                new Keyframe(0.9f, 1.03f),
                new Keyframe(1f, 1f));
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }
                current = next;
            }
        }
    }
}
