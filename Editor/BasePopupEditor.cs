#if UNITY_EDITOR
using System;
using HP.Framework.UI;
using UnityEditor;
using UnityEngine;

namespace HP.Framework.Editor.UI
{
    [CustomEditor(typeof(BasePopup), true)]
    [CanEditMultipleObjects]
    public sealed class BasePopupEditor : UnityEditor.Editor
    {
        private static readonly string[] BackdropNames =
        {
            "Backdrop",
            "BG",
            "Dim",
            "Overlay"
        };

        private static readonly string[] ContentNames =
        {
            "ContentRoot",
            "Content",
            "Panel",
            "PopupContent"
        };

        private SerializedProperty _animationPreset;
        private SerializedProperty _backdropCanvasGroup;
        private SerializedProperty _contentRoot;
        private SerializedProperty _contentCanvasGroup;

        private PreviewSnapshot _previewSnapshot;
        private PopupTransitionPlayer _previewPlayer;
        private bool _previewPlaying;
        private PreviewMode _previewMode = PreviewMode.Show;
        private PreviewPhase _activePreviewPhase = PreviewPhase.Show;
        private float _previewProgress;
        private float _previewSpeed = 1f;
        private bool _previewLoop;
        private bool _autoPreviewOnPresetChange = true;
        private float _phaseElapsed;
        private double _lastPreviewTime;

        private const float CycleHoldDuration = 0.35f;

        private enum PreviewMode
        {
            Show,
            Hide,
            Cycle
        }

        private enum PreviewPhase
        {
            Show,
            HoldAfterShow,
            Hide,
            HoldAfterHide
        }

        private sealed class PreviewSnapshot
        {
            public CanvasGroup Backdrop;
            public RectTransform Content;
            public CanvasGroup ContentCanvas;
            public float BackdropAlpha;
            public Vector3 ContentScale;
            public float ContentAlpha;
        }

        private void OnEnable()
        {
            _animationPreset = serializedObject.FindProperty("animationPreset");
            _backdropCanvasGroup =
                serializedObject.FindProperty("backdropCanvasGroup");
            _contentRoot = serializedObject.FindProperty("contentRoot");
            _contentCanvasGroup =
                serializedObject.FindProperty("contentCanvasGroup");

            EditorApplication.update += UpdatePreview;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdatePreview;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
            ResetPreview();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawScriptReference();
            EditorGUILayout.Space();
            bool presetChanged = DrawAnimationSetup();
            EditorGUILayout.Space();
            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "animationPreset",
                "backdropCanvasGroup",
                "contentRoot",
                "contentCanvasGroup");

            bool propertiesChanged = serializedObject.ApplyModifiedProperties();
            if (propertiesChanged && _previewSnapshot != null)
            {
                ResetPreview();
            }

            if (presetChanged && _autoPreviewOnPresetChange)
            {
                StartPreview(PreviewMode.Show);
            }
        }

        private void DrawScriptReference()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("m_Script"));
            }
        }

        private bool DrawAnimationSetup()
        {
            EditorGUILayout.LabelField("Popup Animation", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_animationPreset);
            bool presetChanged = EditorGUI.EndChangeCheck();
            EditorGUILayout.PropertyField(_backdropCanvasGroup);
            EditorGUILayout.PropertyField(_contentRoot);
            EditorGUILayout.PropertyField(_contentCanvasGroup);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto Bind Targets"))
            {
                AutoBindTargets(false);
            }

            if (GUILayout.Button("Create Missing CanvasGroups"))
            {
                AutoBindTargets(true);
            }
            EditorGUILayout.EndHorizontal();

            DrawValidation();
            DrawPreviewControls();
            return presetChanged;
        }

        private void DrawValidation()
        {
            if (targets.Length != 1)
            {
                EditorGUILayout.HelpBox(
                    "Validation is shown for single-object selection only.",
                    MessageType.Info);
                return;
            }

            UIAnimationPresetSO preset =
                _animationPreset.objectReferenceValue as UIAnimationPresetSO;
            if (preset == null)
            {
                EditorGUILayout.HelpBox(
                    "No animation preset is assigned; Show and Hide are immediate.",
                    MessageType.Info);
                return;
            }

            CanvasGroup backdrop =
                _backdropCanvasGroup.objectReferenceValue as CanvasGroup;
            RectTransform content =
                _contentRoot.objectReferenceValue as RectTransform;
            CanvasGroup contentCanvas =
                _contentCanvasGroup.objectReferenceValue as CanvasGroup;
            BasePopup popup = (BasePopup)target;

            if (preset.UsesBackdropFade && backdrop == null)
            {
                EditorGUILayout.HelpBox(
                    "Backdrop Fade is enabled but Backdrop Canvas Group is missing.",
                    MessageType.Error);
            }
            else if (backdrop != null && backdrop.gameObject == popup.gameObject)
            {
                EditorGUILayout.HelpBox(
                    "Backdrop Canvas Group must be on a child object, not the popup root.",
                    MessageType.Error);
            }

            if (preset.UsesContentScale && content == null)
            {
                EditorGUILayout.HelpBox(
                    "Content Scale is enabled but Content Root is missing.",
                    MessageType.Error);
            }
            else if (content != null && content.gameObject == popup.gameObject)
            {
                EditorGUILayout.HelpBox(
                    "Content Root must be a child object, not the popup root.",
                    MessageType.Error);
            }

            if (preset.UsesContentFade && contentCanvas == null)
            {
                EditorGUILayout.HelpBox(
                    "Content Fade is enabled but Content Canvas Group is missing.",
                    MessageType.Error);
            }
        }

        private void DrawPreviewControls()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Edit Mode Preview", EditorStyles.boldLabel);

            if (targets.Length != 1)
            {
                EditorGUILayout.HelpBox(
                    "Animation preview supports single-object selection only.",
                    MessageType.Info);
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox(
                    "Edit Mode preview is disabled while entering or running Play Mode.",
                    MessageType.Info);
                return;
            }

            bool canPreview = TryGetPreviewContext(
                out _,
                out _,
                out _,
                out _,
                out string error);

            if (!canPreview)
            {
                EditorGUILayout.HelpBox(error, MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(!canPreview))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("▶ Show"))
                {
                    StartPreview(PreviewMode.Show);
                }

                if (GUILayout.Button("▶ Hide"))
                {
                    StartPreview(PreviewMode.Hide);
                }

                if (GUILayout.Button("▶ Show → Hide"))
                {
                    StartPreview(PreviewMode.Cycle);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(!_previewPlaying))
                {
                    if (GUILayout.Button("■ Stop"))
                    {
                        _previewPlaying = false;
                    }
                }

                using (new EditorGUI.DisabledScope(_previewSnapshot == null))
                {
                    if (GUILayout.Button("↺ Reset"))
                    {
                        ResetPreview();
                    }
                }
                EditorGUILayout.EndHorizontal();

                _previewSpeed = EditorGUILayout.Slider(
                    "Preview Speed",
                    _previewSpeed,
                    0.25f,
                    2f);
                _previewLoop = EditorGUILayout.Toggle("Loop", _previewLoop);
                _autoPreviewOnPresetChange = EditorGUILayout.Toggle(
                    "Auto Preview On Preset Change",
                    _autoPreviewOnPresetChange);

                PreviewMode scrubMode = _previewMode == PreviewMode.Hide
                    ? PreviewMode.Hide
                    : PreviewMode.Show;
                EditorGUI.BeginChangeCheck();
                scrubMode = (PreviewMode)EditorGUILayout.EnumPopup(
                    "Scrub Phase",
                    scrubMode);
                if (scrubMode == PreviewMode.Cycle)
                {
                    scrubMode = PreviewMode.Show;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    _previewMode = scrubMode;
                    ScrubPreview(_previewProgress);
                }

                EditorGUILayout.LabelField(
                    "Active Phase",
                    GetActivePhaseLabel());

                EditorGUI.BeginChangeCheck();
                float progress = EditorGUILayout.Slider(
                    "Progress",
                    _previewProgress,
                    0f,
                    1f);
                if (EditorGUI.EndChangeCheck())
                {
                    if (_previewMode == PreviewMode.Cycle)
                    {
                        _previewMode = _activePreviewPhase == PreviewPhase.Hide
                            || _activePreviewPhase == PreviewPhase.HoldAfterHide
                            ? PreviewMode.Hide
                            : PreviewMode.Show;
                    }
                    ScrubPreview(progress);
                }
            }
        }

        private void StartPreview(PreviewMode mode)
        {
            if (!EnsurePreviewSnapshot())
            {
                return;
            }

            _previewMode = mode;
            _previewPlaying = true;
            _lastPreviewTime = EditorApplication.timeSinceStartup;
            BeginPhase(mode == PreviewMode.Hide
                ? PreviewPhase.Hide
                : PreviewPhase.Show);
        }

        private void ScrubPreview(float progress)
        {
            if (!EnsurePreviewSnapshot())
            {
                return;
            }

            _previewPlaying = false;
            PreviewPhase phase = _previewMode == PreviewMode.Hide
                ? PreviewPhase.Hide
                : PreviewPhase.Show;
            BeginPhase(phase);
            _previewPlaying = false;
            _previewProgress = Mathf.Clamp01(progress);
            ApplyPreviewFrame();
        }

        private void UpdatePreview()
        {
            if (!_previewPlaying || _previewSnapshot == null)
            {
                return;
            }

            if (!TryGetPreviewContext(
                out UIAnimationPresetSO preset,
                out _,
                out _,
                out _,
                out _))
            {
                ResetPreview();
                return;
            }

            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentTime - _lastPreviewTime);
            _lastPreviewTime = currentTime;

            float scaledDeltaTime = deltaTime * Mathf.Max(0.01f, _previewSpeed);
            AdvancePreview(preset, scaledDeltaTime);
            Repaint();
        }

        private void ApplyPreviewFrame()
        {
            if (_previewSnapshot == null || _previewPlayer == null)
            {
                return;
            }

            if (!TryGetPreviewContext(
                out UIAnimationPresetSO preset,
                out _,
                out _,
                out _,
                out _))
            {
                return;
            }

            if (_activePreviewPhase == PreviewPhase.Show)
            {
                _previewPlayer.ApplyShow(preset.Show.TotalDuration * _previewProgress);
            }
            else if (_activePreviewPhase == PreviewPhase.Hide)
            {
                _previewPlayer.ApplyHide(preset.Hide.TotalDuration * _previewProgress);
            }

            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private void AdvancePreview(UIAnimationPresetSO preset, float deltaTime)
        {
            _phaseElapsed += deltaTime;
            switch (_activePreviewPhase)
            {
                case PreviewPhase.Show:
                {
                    float duration = preset.Show.TotalDuration;
                    _previewProgress = duration <= 0f
                        ? 1f
                        : Mathf.Clamp01(_phaseElapsed / duration);
                    ApplyPreviewFrame();
                    if (_previewProgress >= 1f)
                    {
                        if (_previewMode == PreviewMode.Cycle)
                        {
                            BeginHold(PreviewPhase.HoldAfterShow);
                        }
                        else if (_previewLoop)
                        {
                            BeginPhase(PreviewPhase.Show);
                        }
                        else
                        {
                            _previewPlaying = false;
                        }
                    }
                    break;
                }
                case PreviewPhase.Hide:
                {
                    float duration = preset.Hide.TotalDuration;
                    _previewProgress = duration <= 0f
                        ? 1f
                        : Mathf.Clamp01(_phaseElapsed / duration);
                    ApplyPreviewFrame();
                    if (_previewProgress >= 1f)
                    {
                        if (_previewMode == PreviewMode.Cycle && _previewLoop)
                        {
                            BeginHold(PreviewPhase.HoldAfterHide);
                        }
                        else if (_previewMode == PreviewMode.Hide && _previewLoop)
                        {
                            BeginPhase(PreviewPhase.Hide);
                        }
                        else
                        {
                            _previewPlaying = false;
                        }
                    }
                    break;
                }
                case PreviewPhase.HoldAfterShow:
                    if (_phaseElapsed >= CycleHoldDuration)
                    {
                        BeginPhase(PreviewPhase.Hide, restoreShownState: false);
                    }
                    break;
                case PreviewPhase.HoldAfterHide:
                    if (_phaseElapsed >= CycleHoldDuration)
                    {
                        BeginPhase(PreviewPhase.Show);
                    }
                    break;
            }
        }

        private void BeginPhase(
            PreviewPhase phase,
            bool restoreShownState = true)
        {
            if (_previewSnapshot == null
                || !TryGetPreviewContext(
                    out UIAnimationPresetSO preset,
                    out CanvasGroup backdrop,
                    out RectTransform content,
                    out CanvasGroup contentCanvas,
                    out _))
            {
                return;
            }

            if (restoreShownState)
            {
                RestorePreviewValues();
            }

            _previewPlayer = new PopupTransitionPlayer(
                backdrop,
                content,
                contentCanvas,
                preset);
            _activePreviewPhase = phase;
            _phaseElapsed = 0f;
            _previewProgress = 0f;

            if (phase == PreviewPhase.Show)
            {
                _previewPlayer.BeginShow(false);
                _previewPlayer.ApplyShow(0f);
            }
            else if (phase == PreviewPhase.Hide)
            {
                _previewPlayer.BeginHide();
                _previewPlayer.ApplyHide(0f);
            }

            SceneView.RepaintAll();
        }

        private void BeginHold(PreviewPhase phase)
        {
            _activePreviewPhase = phase;
            _phaseElapsed = 0f;
            _previewProgress = 1f;
        }

        private string GetActivePhaseLabel()
        {
            return _activePreviewPhase switch
            {
                PreviewPhase.Show => "Show",
                PreviewPhase.Hide => "Hide",
                PreviewPhase.HoldAfterShow => "Show Complete (hold)",
                PreviewPhase.HoldAfterHide => "Hide Complete (hold)",
                _ => "Show"
            };
        }

        private bool EnsurePreviewSnapshot()
        {
            if (!TryGetPreviewContext(
                out _,
                out CanvasGroup backdrop,
                out RectTransform content,
                out CanvasGroup contentCanvas,
                out _))
            {
                return false;
            }

            if (_previewSnapshot != null
                && _previewSnapshot.Backdrop == backdrop
                && _previewSnapshot.Content == content
                && _previewSnapshot.ContentCanvas == contentCanvas)
            {
                return true;
            }

            ResetPreview();
            _previewSnapshot = new PreviewSnapshot
            {
                Backdrop = backdrop,
                Content = content,
                ContentCanvas = contentCanvas,
                BackdropAlpha = backdrop != null ? backdrop.alpha : 1f,
                ContentScale = content != null
                    ? content.localScale
                    : Vector3.one,
                ContentAlpha = contentCanvas != null
                    ? contentCanvas.alpha
                    : 1f
            };
            return true;
        }

        private void ResetPreview()
        {
            _previewPlaying = false;
            RestorePreviewValues();
            _previewPlayer = null;
            _previewSnapshot = null;
            _previewProgress = 0f;
            _phaseElapsed = 0f;
            SceneView.RepaintAll();
        }

        private void RestorePreviewValues()
        {
            if (_previewSnapshot == null)
            {
                return;
            }

            if (_previewSnapshot.Backdrop != null)
            {
                _previewSnapshot.Backdrop.alpha =
                    _previewSnapshot.BackdropAlpha;
            }

            if (_previewSnapshot.Content != null)
            {
                _previewSnapshot.Content.localScale =
                    _previewSnapshot.ContentScale;
            }

            if (_previewSnapshot.ContentCanvas != null)
            {
                _previewSnapshot.ContentCanvas.alpha =
                    _previewSnapshot.ContentAlpha;
            }
        }

        private bool TryGetPreviewContext(
            out UIAnimationPresetSO preset,
            out CanvasGroup backdrop,
            out RectTransform content,
            out CanvasGroup contentCanvas,
            out string error)
        {
            preset = _animationPreset.objectReferenceValue as UIAnimationPresetSO;
            backdrop = _backdropCanvasGroup.objectReferenceValue as CanvasGroup;
            content = _contentRoot.objectReferenceValue as RectTransform;
            contentCanvas =
                _contentCanvasGroup.objectReferenceValue as CanvasGroup;

            if (preset == null)
            {
                error = "Assign an animation preset to enable preview.";
                return false;
            }

            BasePopup popup = target as BasePopup;
            if (popup == null || !popup.gameObject.activeInHierarchy)
            {
                error = "The popup must be active in the hierarchy to preview animation.";
                return false;
            }

            if (preset.UsesBackdropFade && backdrop == null)
            {
                error = "Assign the Backdrop Canvas Group before previewing.";
                return false;
            }

            if (preset.UsesContentScale && content == null)
            {
                error = "Assign the Content Root before previewing.";
                return false;
            }

            if (preset.UsesContentFade && contentCanvas == null)
            {
                error = "Assign the Content Canvas Group before previewing.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void BeforeAssemblyReload()
        {
            ResetPreview();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode
                || state == PlayModeStateChange.EnteredPlayMode)
            {
                ResetPreview();
            }
        }

        private void AutoBindTargets(bool createMissingCanvasGroups)
        {
            ResetPreview();
            foreach (UnityEngine.Object selectedTarget in targets)
            {
                BasePopup popup = (BasePopup)selectedTarget;
                SerializedObject popupObject = new SerializedObject(popup);
                SerializedProperty backdropProperty =
                    popupObject.FindProperty("backdropCanvasGroup");
                SerializedProperty contentProperty =
                    popupObject.FindProperty("contentRoot");
                SerializedProperty contentCanvasProperty =
                    popupObject.FindProperty("contentCanvasGroup");

                RectTransform backdropTransform = FindNamedChild(
                    popup.transform,
                    BackdropNames);
                RectTransform contentTransform = FindNamedChild(
                    popup.transform,
                    ContentNames);

                CanvasGroup backdropGroup = GetOrCreateCanvasGroup(
                    backdropTransform,
                    createMissingCanvasGroups);
                CanvasGroup contentGroup = GetOrCreateCanvasGroup(
                    contentTransform,
                    createMissingCanvasGroups);

                Undo.RecordObject(popup, "Bind Popup Animation Targets");
                backdropProperty.objectReferenceValue = backdropGroup;
                contentProperty.objectReferenceValue = contentTransform;
                contentCanvasProperty.objectReferenceValue = contentGroup;
                popupObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(popup);
            }

            serializedObject.Update();
        }

        private static CanvasGroup GetOrCreateCanvasGroup(
            RectTransform target,
            bool createMissing)
        {
            if (target == null)
            {
                return null;
            }

            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null && createMissing)
            {
                canvasGroup = Undo.AddComponent<CanvasGroup>(target.gameObject);
            }

            return canvasGroup;
        }

        private static RectTransform FindNamedChild(
            Transform root,
            string[] candidateNames)
        {
            RectTransform[] transforms =
                root.GetComponentsInChildren<RectTransform>(true);
            for (int nameIndex = 0; nameIndex < candidateNames.Length; nameIndex++)
            {
                string candidateName = candidateNames[nameIndex];
                for (int transformIndex = 0;
                    transformIndex < transforms.Length;
                    transformIndex++)
                {
                    RectTransform candidate = transforms[transformIndex];
                    if (candidate.transform == root)
                    {
                        continue;
                    }

                    if (string.Equals(
                        candidate.name,
                        candidateName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }
    }
}
#endif


