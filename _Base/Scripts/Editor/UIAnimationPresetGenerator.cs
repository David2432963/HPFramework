using UnityEngine;
using UnityEditor;
using Base.UI;
using DG.Tweening;

namespace Base.Editor.UI
{
    public static class UIAnimationPresetGenerator
    {
        [MenuItem("Tools/Keyboard Escape/Generate Base UI Animations")]
        public static void GeneratePresets()
        {
            string folderPath = "Assets/_Base/Datas/UI/Animations";
            if (!AssetDatabase.IsValidFolder("Assets/_Base/Datas"))
                AssetDatabase.CreateFolder("Assets/_Base", "Datas");
            if (!AssetDatabase.IsValidFolder("Assets/_Base/Datas/UI"))
                AssetDatabase.CreateFolder("Assets/_Base/Datas", "UI");
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder("Assets/_Base/Datas/UI", "Animations");

            CreatePreset(folderPath, "Anim_FadeAndScale", UIAnimationType.FadeAndScale, 0.3f, Ease.OutBack, new Vector2(0, 0), 0.7f,
                                                        UIAnimationType.FadeAndScale, 0.2f, Ease.InBack, new Vector2(0, 0), 0.7f);

            CreatePreset(folderPath, "Anim_SlideUpFade", UIAnimationType.SlideAndFade, 0.4f, Ease.OutCubic, new Vector2(0, -200f), 1f,
                                                       UIAnimationType.SlideAndFade, 0.25f, Ease.InCubic, new Vector2(0, -200f), 1f);

            CreatePreset(folderPath, "Anim_ElasticPop", UIAnimationType.ElasticScale, 0.6f, Ease.OutElastic, new Vector2(0, 0), 0f,
                                                      UIAnimationType.Scale, 0.2f, Ease.InBack, new Vector2(0, 0), 0f);

            CreatePreset(folderPath, "Anim_PunchFocus", UIAnimationType.PunchScale, 0.4f, Ease.OutQuad, new Vector2(0, 0), 1f,
                                                      UIAnimationType.FadeAndScale, 0.2f, Ease.InQuad, new Vector2(0, 0), 0.8f);

            CreatePreset(folderPath, "Anim_SlideRightFade", UIAnimationType.SlideAndFade, 0.35f, Ease.OutQuart, new Vector2(-300f, 0), 1f,
                                                          UIAnimationType.SlideAndFade, 0.25f, Ease.InQuart, new Vector2(-300f, 0), 1f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UI] Successfully generated UI Animation Presets in {folderPath}");
        }

        private static void CreatePreset(string folder, string name, 
            UIAnimationType showType, float showDur, Ease showEase, Vector2 showOffset, float showScale,
            UIAnimationType hideType, float hideDur, Ease hideEase, Vector2 hideOffset, float hideScale)
        {
            string path = $"{folder}/{name}.asset";
            UIAnimationPresetSO preset = AssetDatabase.LoadAssetAtPath<UIAnimationPresetSO>(path);
            
            bool isNew = false;
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<UIAnimationPresetSO>();
                isNew = true;
            }

            preset.ShowType = showType;
            preset.ShowDuration = showDur;
            preset.ShowEase = showEase;
            preset.ShowStartOffset = showOffset;
            preset.ShowStartScale = showScale;

            preset.HideType = hideType;
            preset.HideDuration = hideDur;
            preset.HideEase = hideEase;
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
    }
}
