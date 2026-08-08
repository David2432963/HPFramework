#if UNITY_EDITOR
using HP.Framework.UI;
using UnityEditor;
using UnityEngine;

namespace HP.Framework.Editor.UI
{
    [CustomEditor(typeof(UIAnimationPresetSO))]
    public sealed class UIAnimationPresetSOEditor : UnityEditor.Editor
    {
        private SerializedProperty _show;
        private SerializedProperty _hide;
        private bool _showExpanded = true;
        private bool _hideExpanded = true;

        private void OnEnable()
        {
            _show = serializedObject.FindProperty("Show");
            _hide = serializedObject.FindProperty("Hide");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawScriptReference();
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Backdrop and content are animated independently. Curves use normalized time and value ranges from 0 to 1; overshoot values are allowed.",
                MessageType.Info);

            _showExpanded = DrawPhase("Show", _show, _showExpanded, true);
            EditorGUILayout.Space();
            _hideExpanded = DrawPhase("Hide", _hide, _hideExpanded, false);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScriptReference()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "Script",
                    MonoScript.FromScriptableObject((UIAnimationPresetSO)target),
                    typeof(MonoScript),
                    false);
            }
        }

        private bool DrawPhase(
            string label,
            SerializedProperty phase,
            bool expanded,
            bool isShow)
        {
            expanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, label);
            if (expanded)
            {
                EditorGUI.indentLevel++;
                DrawFadeTrack(
                    "Backdrop Fade",
                    phase.FindPropertyRelative("BackdropFade"));
                DrawScaleTrack(
                    "Content Scale",
                    phase.FindPropertyRelative("ContentScale"),
                    isShow ? "Start Scale" : "End Scale");
                DrawFadeTrack(
                    "Content Fade",
                    phase.FindPropertyRelative("ContentFade"));
                EditorGUI.indentLevel--;

                EditorGUILayout.HelpBox(
                    $"Phase duration: {CalculateTotalDuration(phase):0.###} seconds",
                    MessageType.None);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            return expanded;
        }

        private void DrawFadeTrack(
            string label,
            SerializedProperty transition)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            SerializedProperty enabled = transition.FindPropertyRelative("Enabled");
            EditorGUILayout.PropertyField(enabled);
            if (!enabled.boolValue)
            {
                return;
            }

            DrawTiming(transition.FindPropertyRelative("Timing"));
            EditorGUILayout.Space(2f);
        }

        private void DrawScaleTrack(
            string label,
            SerializedProperty transition,
            string scaleLabel)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            SerializedProperty enabled = transition.FindPropertyRelative("Enabled");
            EditorGUILayout.PropertyField(enabled);
            if (!enabled.boolValue)
            {
                return;
            }

            EditorGUILayout.PropertyField(
                transition.FindPropertyRelative("Scale"),
                new GUIContent(scaleLabel));
            DrawTiming(transition.FindPropertyRelative("Timing"));
            EditorGUILayout.Space(2f);
        }

        private void DrawTiming(SerializedProperty timing)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(timing.FindPropertyRelative("Delay"));
            EditorGUILayout.PropertyField(timing.FindPropertyRelative("Duration"));

            SerializedProperty useCustomCurve =
                timing.FindPropertyRelative("UseCustomCurve");
            EditorGUILayout.PropertyField(useCustomCurve);
            if (useCustomCurve.boolValue)
            {
                SerializedProperty curve = timing.FindPropertyRelative("Curve");
                EditorGUILayout.PropertyField(
                    curve,
                    new GUIContent("Curve"),
                    GUILayout.Height(52f));
                DrawCurvePresetButtons(curve);
                DrawCurveWarnings(curve.animationCurveValue);
            }
            else
            {
                EditorGUILayout.PropertyField(timing.FindPropertyRelative("Ease"));
            }

            EditorGUI.indentLevel--;
        }

        private void DrawCurvePresetButtons(SerializedProperty curve)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Linear"))
            {
                curve.animationCurveValue =
                    AnimationCurve.Linear(0f, 0f, 1f, 1f);
            }

            if (GUILayout.Button("Smooth"))
            {
                curve.animationCurveValue =
                    AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }

            if (GUILayout.Button("Out Back"))
            {
                curve.animationCurveValue = CreateOutBackCurve();
            }

            if (GUILayout.Button("In Back"))
            {
                curve.animationCurveValue = CreateInBackCurve();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawCurveWarnings(AnimationCurve curve)
        {
            if (curve == null || curve.length < 2)
            {
                EditorGUILayout.HelpBox(
                    "Curve requires at least two keys.",
                    MessageType.Error);
                return;
            }

            Keyframe first = curve.keys[0];
            Keyframe last = curve.keys[curve.length - 1];
            if (!Mathf.Approximately(first.time, 0f)
                || !Mathf.Approximately(last.time, 1f))
            {
                EditorGUILayout.HelpBox(
                    "Curve time should run from 0 to 1.",
                    MessageType.Warning);
            }

            if (!Mathf.Approximately(first.value, 0f)
                || !Mathf.Approximately(last.value, 1f))
            {
                EditorGUILayout.HelpBox(
                    "Curve should normally start at 0 and end at 1. Overshoot values between those keys are allowed.",
                    MessageType.Warning);
            }
        }

        private static float CalculateTotalDuration(SerializedProperty phase)
        {
            return Mathf.Max(
                GetTrackDuration(phase.FindPropertyRelative("BackdropFade")),
                Mathf.Max(
                    GetTrackDuration(phase.FindPropertyRelative("ContentScale")),
                    GetTrackDuration(phase.FindPropertyRelative("ContentFade"))));
        }

        private static float GetTrackDuration(SerializedProperty transition)
        {
            if (!transition.FindPropertyRelative("Enabled").boolValue)
            {
                return 0f;
            }

            SerializedProperty timing = transition.FindPropertyRelative("Timing");
            return Mathf.Max(0f, timing.FindPropertyRelative("Delay").floatValue)
                + Mathf.Max(0f, timing.FindPropertyRelative("Duration").floatValue);
        }

        private static AnimationCurve CreateOutBackCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.65f, 1.08f, 0.4f, 0f),
                new Keyframe(1f, 1f, 0f, 0f));
        }

        private static AnimationCurve CreateInBackCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.35f, -0.08f, 0f, 0.4f),
                new Keyframe(1f, 1f, 0f, 0f));
        }
    }
}
#endif


