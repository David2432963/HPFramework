using System;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("HP.Framework.Editor")]
[assembly: InternalsVisibleTo("HP.Framework.Tests.Editor")]

namespace HP.Framework.UI
{
    /// <summary>
    /// Evaluates popup backdrop/content transitions without introducing a hard tween dependency.
    /// Supports independent delay/duration/ease tracks and interrupted show/hide transitions.
    /// </summary>
    internal sealed class PopupTransitionPlayer
    {
        private readonly CanvasGroup backdropCanvasGroup;
        private readonly RectTransform contentRoot;
        private readonly CanvasGroup contentCanvasGroup;
        private readonly UIAnimationPresetSO preset;
        private readonly float shownBackdropAlpha;
        private readonly Vector3 shownContentScale;
        private readonly float shownContentAlpha;

        private float startBackdropAlpha;
        private Vector3 startContentScale;
        private float startContentAlpha;

        public PopupTransitionPlayer(
            CanvasGroup backdropCanvasGroup,
            RectTransform contentRoot,
            CanvasGroup contentCanvasGroup,
            UIAnimationPresetSO preset)
        {
            this.backdropCanvasGroup = backdropCanvasGroup;
            this.contentRoot = contentRoot;
            this.contentCanvasGroup = contentCanvasGroup;
            this.preset = preset ?? throw new ArgumentNullException(nameof(preset));

            ValidateDependencies();
            shownBackdropAlpha = backdropCanvasGroup != null ? backdropCanvasGroup.alpha : 1f;
            shownContentScale = contentRoot != null ? contentRoot.localScale : Vector3.one;
            shownContentAlpha = contentCanvasGroup != null ? contentCanvasGroup.alpha : 1f;
        }

        public float BeginShow(bool preserveCurrentState)
        {
            UIAnimationPresetSO.TransitionPhase phase = preset.Show;
            if (!preserveCurrentState)
            {
                ApplyShowStartState(phase);
            }
            else
            {
                ApplyShownValuesForDisabledTracks(phase);
            }

            CaptureCurrentState();
            return phase.TotalDuration;
        }

        public void ApplyShow(float elapsedTime)
        {
            UIAnimationPresetSO.TransitionPhase phase = preset.Show;

            if (phase.BackdropFade.Enabled)
            {
                float t = UIAnimationPresetSO.EvaluateTiming(phase.BackdropFade.Timing, elapsedTime);
                backdropCanvasGroup.alpha = Mathf.LerpUnclamped(startBackdropAlpha, shownBackdropAlpha, t);
            }

            if (phase.ContentScale.Enabled)
            {
                float t = UIAnimationPresetSO.EvaluateTiming(phase.ContentScale.Timing, elapsedTime);
                contentRoot.localScale = Vector3.LerpUnclamped(startContentScale, shownContentScale, t);
            }

            if (phase.ContentFade.Enabled)
            {
                float t = UIAnimationPresetSO.EvaluateTiming(phase.ContentFade.Timing, elapsedTime);
                contentCanvasGroup.alpha = Mathf.LerpUnclamped(startContentAlpha, shownContentAlpha, t);
            }
        }

        public float BeginHide()
        {
            CaptureCurrentState();
            return preset.Hide.TotalDuration;
        }

        public void ApplyHide(float elapsedTime)
        {
            UIAnimationPresetSO.TransitionPhase phase = preset.Hide;

            if (phase.BackdropFade.Enabled)
            {
                float t = UIAnimationPresetSO.EvaluateTiming(phase.BackdropFade.Timing, elapsedTime);
                backdropCanvasGroup.alpha = Mathf.LerpUnclamped(startBackdropAlpha, 0f, t);
            }

            if (phase.ContentScale.Enabled)
            {
                float t = UIAnimationPresetSO.EvaluateTiming(phase.ContentScale.Timing, elapsedTime);
                Vector3 targetScale = shownContentScale * phase.ContentScale.Scale;
                contentRoot.localScale = Vector3.LerpUnclamped(startContentScale, targetScale, t);
            }

            if (phase.ContentFade.Enabled)
            {
                float t = UIAnimationPresetSO.EvaluateTiming(phase.ContentFade.Timing, elapsedTime);
                contentCanvasGroup.alpha = Mathf.LerpUnclamped(startContentAlpha, 0f, t);
            }
        }

        public void ApplyShownState()
        {
            if (backdropCanvasGroup != null)
            {
                backdropCanvasGroup.alpha = shownBackdropAlpha;
            }

            if (contentRoot != null)
            {
                contentRoot.localScale = shownContentScale;
            }

            if (contentCanvasGroup != null)
            {
                contentCanvasGroup.alpha = shownContentAlpha;
            }
        }

        private void ApplyShowStartState(UIAnimationPresetSO.TransitionPhase phase)
        {
            if (backdropCanvasGroup != null)
            {
                backdropCanvasGroup.alpha = phase.BackdropFade.Enabled ? 0f : shownBackdropAlpha;
            }

            if (contentRoot != null)
            {
                contentRoot.localScale = phase.ContentScale.Enabled
                    ? shownContentScale * phase.ContentScale.Scale
                    : shownContentScale;
            }

            if (contentCanvasGroup != null)
            {
                contentCanvasGroup.alpha = phase.ContentFade.Enabled ? 0f : shownContentAlpha;
            }
        }

        private void ApplyShownValuesForDisabledTracks(UIAnimationPresetSO.TransitionPhase phase)
        {
            if (!phase.BackdropFade.Enabled && backdropCanvasGroup != null)
            {
                backdropCanvasGroup.alpha = shownBackdropAlpha;
            }

            if (!phase.ContentScale.Enabled && contentRoot != null)
            {
                contentRoot.localScale = shownContentScale;
            }

            if (!phase.ContentFade.Enabled && contentCanvasGroup != null)
            {
                contentCanvasGroup.alpha = shownContentAlpha;
            }
        }

        private void CaptureCurrentState()
        {
            startBackdropAlpha = backdropCanvasGroup != null ? backdropCanvasGroup.alpha : shownBackdropAlpha;
            startContentScale = contentRoot != null ? contentRoot.localScale : shownContentScale;
            startContentAlpha = contentCanvasGroup != null ? contentCanvasGroup.alpha : shownContentAlpha;
        }

        private void ValidateDependencies()
        {
            if (preset.UsesBackdropFade && backdropCanvasGroup == null)
            {
                throw new MissingReferenceException(
                    "UI animation preset uses backdrop fade but no backdrop CanvasGroup is assigned.");
            }

            if (preset.UsesContentScale && contentRoot == null)
            {
                throw new MissingReferenceException(
                    "UI animation preset uses content scale but no content root is assigned.");
            }

            if (preset.UsesContentFade && contentCanvasGroup == null)
            {
                throw new MissingReferenceException(
                    "UI animation preset uses content fade but no content CanvasGroup is assigned.");
            }
        }
    }
}


