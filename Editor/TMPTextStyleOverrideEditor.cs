#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using HP.Framework.UI.TMP;

namespace HP.Framework.Editor
{
    [CustomEditor(typeof(TMPTextStyleOverride))]
    public sealed class TMPTextStyleOverrideEditor : UnityEditor.Editor
    {
        private SerializedProperty _mode;
        private SerializedProperty _overrideEnabled;
        private SerializedProperty _sharedPreset;

        private void OnEnable()
        {
            _mode = serializedObject.FindProperty("_materialMode");
            _overrideEnabled = serializedObject.FindProperty("_overrideEnabled");
            _sharedPreset = serializedObject.FindProperty("_sharedMaterialPreset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_mode);
            EditorGUILayout.PropertyField(_overrideEnabled, new GUIContent("Override Enabled"));

            TMPTextStyleOverride style = (TMPTextStyleOverride)target;
            if (style.Mode == TMPTextStyleOverride.MaterialMode.SharedMaterialPreset)
            {
                EditorGUILayout.PropertyField(_sharedPreset, new GUIContent("Material Preset"));
                EditorGUILayout.HelpBox(
                    "This mode intentionally shares changes with every text using this material preset.",
                    MessageType.Info);
            }
            else
            {
                DrawPropertiesExcluding(
                    serializedObject,
                    "m_Script",
                    "_materialMode",
                    "_overrideEnabled",
                    "_baseSharedMaterial",
                    "_sharedMaterialPreset");
            }

            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("Apply Style"))
            {
                ApplyToTargets();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Enable Override"))
                {
                    foreach (Object targetObject in targets)
                    {
                        Undo.RecordObject(targetObject, "Enable TMP Style Override");
                        ((TMPTextStyleOverride)targetObject).EnableOverride();
                        EditorUtility.SetDirty(targetObject);
                    }
                }

                if (GUILayout.Button("Restore Base"))
                {
                    foreach (Object targetObject in targets)
                    {
                        Undo.RecordObject(targetObject, "Restore TMP Base Material");
                        ((TMPTextStyleOverride)targetObject).RestoreBaseMaterial();
                        EditorUtility.SetDirty(targetObject);
                    }
                }
            }

            if (GUILayout.Button("Rebase From Current Material"))
            {
                foreach (Object targetObject in targets)
                {
                    Undo.RecordObject(targetObject, "Rebase TMP Style Material");
                    ((TMPTextStyleOverride)targetObject).RebaseFromCurrentMaterial();
                    EditorUtility.SetDirty(targetObject);
                }
            }

            if (style.Mode == TMPTextStyleOverride.MaterialMode.UniqueInstance
                && GUILayout.Button("Create Material Preset From Current"))
            {
                CreatePreset(style);
            }
        }

        private void ApplyToTargets()
        {
            foreach (Object targetObject in targets)
            {
                Undo.RecordObject(targetObject, "Apply TMP Text Style");
                ((TMPTextStyleOverride)targetObject).ApplyStyle();
                EditorUtility.SetDirty(targetObject);
            }
        }

        private static void CreatePreset(TMPTextStyleOverride style)
        {
            Material source = style.CurrentMaterial;
            if (source == null)
            {
                EditorUtility.DisplayDialog(
                    "TMP Material Preset",
                    "The text does not have a material instance to copy.",
                    "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "Create TMP Material Preset",
                source.name + " Preset",
                "mat",
                "Choose where to save the TMP material preset.");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            Material preset = new Material(source)
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path)
            };
            AssetDatabase.CreateAsset(preset, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();

            Undo.RecordObject(style, "Assign TMP Material Preset");
            SerializedObject serializedStyle = new SerializedObject(style);
            serializedStyle.FindProperty("_materialMode").enumValueIndex =
                (int)TMPTextStyleOverride.MaterialMode.SharedMaterialPreset;
            serializedStyle.FindProperty("_sharedMaterialPreset").objectReferenceValue = preset;
            serializedStyle.FindProperty("_overrideEnabled").boolValue = true;
            serializedStyle.ApplyModifiedProperties();
            style.ApplyStyle();
            EditorUtility.SetDirty(style);
            Selection.activeObject = preset;
        }
    }
}
#endif
