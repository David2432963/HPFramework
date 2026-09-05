#if UNITY_EDITOR
using HP.Framework.UI.TMP;
using UnityEditor;
using UnityEngine;

namespace HP.Framework.Editor
{
    [CustomEditor(typeof(TMPTextStyleOverride))]
    public sealed class TMPTextStyleOverrideEditor : UnityEditor.Editor
    {
        private SerializedProperty _effectsEnabled;
        private SerializedProperty _outlineEnabled;
        private SerializedProperty _outlineWidth;
        private SerializedProperty _outlineColor;
        private SerializedProperty _shadowEnabled;
        private SerializedProperty _shadowColor;
        private SerializedProperty _shadowOffsetX;
        private SerializedProperty _shadowOffsetY;
        private SerializedProperty _shadowDilate;
        private SerializedProperty _shadowSoftness;
        private SerializedProperty _glowEnabled;
        private SerializedProperty _glowColor;
        private SerializedProperty _glowOffset;
        private SerializedProperty _glowInner;
        private SerializedProperty _glowOuter;
        private SerializedProperty _glowPower;
        private SerializedProperty _serializedVersion;

        private void OnEnable()
        {
            _effectsEnabled = serializedObject.FindProperty("_overrideEnabled");
            _outlineEnabled = serializedObject.FindProperty("_outlineEnabled");
            _outlineWidth = serializedObject.FindProperty("_outlineWidth");
            _outlineColor = serializedObject.FindProperty("_outlineColor");
            _shadowEnabled = serializedObject.FindProperty("_underlayEnabled");
            _shadowColor = serializedObject.FindProperty("_underlayColor");
            _shadowOffsetX = serializedObject.FindProperty("_underlayOffsetX");
            _shadowOffsetY = serializedObject.FindProperty("_underlayOffsetY");
            _shadowDilate = serializedObject.FindProperty("_underlayDilate");
            _shadowSoftness = serializedObject.FindProperty("_underlaySoftness");
            _glowEnabled = serializedObject.FindProperty("_glowEnabled");
            _glowColor = serializedObject.FindProperty("_glowColor");
            _glowOffset = serializedObject.FindProperty("_glowOffset");
            _glowInner = serializedObject.FindProperty("_glowInner");
            _glowOuter = serializedObject.FindProperty("_glowOuter");
            _glowPower = serializedObject.FindProperty("_glowPower");
            _serializedVersion = serializedObject.FindProperty("_serializedVersion");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_effectsEnabled, new GUIContent("Effects Enabled"));
            EditorGUILayout.Space();

            DrawOutline();
            DrawShadow();
            DrawGlow();

            if (serializedObject.ApplyModifiedProperties())
            {
                foreach (Object targetObject in targets)
                {
                    ((TMPTextStyleOverride)targetObject).ApplyStyle();
                    EditorUtility.SetDirty(targetObject);
                }
            }
        }

        private void DrawOutline()
        {
            DrawSection("Outline", _outlineEnabled, out bool enabled, _serializedVersion.intValue == 0 && _outlineWidth.floatValue > 0f);
            if (enabled)
            {
                EditorGUILayout.PropertyField(_outlineWidth);
                EditorGUILayout.PropertyField(_outlineColor);
            }
        }

        private void DrawShadow()
        {
            DrawSection("Shadow", _shadowEnabled, out bool enabled, _shadowEnabled.boolValue);
            if (enabled)
            {
                EditorGUILayout.PropertyField(_shadowColor);
                EditorGUILayout.PropertyField(_shadowOffsetX, new GUIContent("Offset X"));
                EditorGUILayout.PropertyField(_shadowOffsetY, new GUIContent("Offset Y"));
                EditorGUILayout.PropertyField(_shadowDilate);
                EditorGUILayout.PropertyField(_shadowSoftness);
            }
        }

        private void DrawGlow()
        {
            DrawSection("Glow", _glowEnabled, out bool enabled, _glowEnabled.boolValue);
            if (enabled)
            {
                EditorGUILayout.PropertyField(_glowColor);
                EditorGUILayout.PropertyField(_glowOffset);
                EditorGUILayout.PropertyField(_glowInner);
                EditorGUILayout.PropertyField(_glowOuter);
                EditorGUILayout.PropertyField(_glowPower);
            }
        }

        private void DrawSection(string label, SerializedProperty enabledProperty, out bool enabled, bool fallbackValue)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                enabled = EditorGUILayout.ToggleLeft(label, fallbackValue);
                if (EditorGUI.EndChangeCheck())
                {
                    enabledProperty.boolValue = enabled;
                    if (enabledProperty == _outlineEnabled)
                    {
                        _serializedVersion.intValue = 1;
                    }
                }
            }
        }
    }
}
#endif
