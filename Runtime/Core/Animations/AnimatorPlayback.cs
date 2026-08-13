using System;
using UnityEngine;

namespace HP.Framework.Animations
{
    #region Types

    public enum AnimatorInterruptPolicy
    {
        Force,
        Ignore,
        Restart
    }

    #endregion

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class AnimatorPlayback : MonoBehaviour
    {
        [Header("Defaults")]
        [SerializeField, Min(0)] private int defaultLayer = 0;
        [SerializeField] private string defaultStateName = "Idle";

        #region Runtime State
        private Animator animator;
        private string activeStateName;
        private string returnStateName;
        private Action<string> startedCallback;
        private Action<string> completedCallback;
        private int activeStateHash;
        private int activeLayer;
        private bool activeLoop;
        private bool activeHoldFinalFrame;
        private bool isPlaying;
        private bool holdingFinalFrame;
        private bool completionRaised;
        private int previousStateHash;
        private int previousLayer;
        private float previousNormalizedTime;
        private bool hasPreviousState;

        #endregion

        public Animator Animator => animator;
        public string CurrentStateName => activeStateName;

        #region Unity Lifecycle

        private void Reset()
        {
            animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }

        private void Update()
        {
            if (animator == null || (!isPlaying && !holdingFinalFrame))
            {
                return;
            }

            if (holdingFinalFrame)
            {
                animator.Play(activeStateHash, activeLayer, 0.9999f);
                return;
            }

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(activeLayer);
            if (activeLoop || animator.IsInTransition(activeLayer))
            {
                return;
            }

            if (stateInfo.fullPathHash != activeStateHash || stateInfo.normalizedTime < 1f)
            {
                return;
            }

            CompleteAnimation();
        }

        private void OnDisable()
        {
            ClearPlaybackState();
        }

        #endregion

        #region Public API

        public bool Play(
            string stateName,
            int layer = 0,
            bool loop = true,
            bool holdFinalFrame = false,
            float normalizedTime = 0f,
            AnimatorInterruptPolicy interruptPolicy = AnimatorInterruptPolicy.Force,
            Action<string> onStarted = null,
            Action<string> onCompleted = null)
        {
            return StartPlayback(
                stateName,
                layer,
                loop,
                holdFinalFrame,
                normalizedTime,
                0f,
                null,
                interruptPolicy,
                onStarted,
                onCompleted);
        }

        public bool CrossFade(
            string stateName,
            float transitionDuration,
            int layer = 0,
            bool loop = true,
            bool holdFinalFrame = false,
            float normalizedTime = 0f,
            AnimatorInterruptPolicy interruptPolicy = AnimatorInterruptPolicy.Force,
            Action<string> onStarted = null,
            Action<string> onCompleted = null)
        {
            return StartPlayback(
                stateName,
                layer,
                loop,
                holdFinalFrame,
                normalizedTime,
                Mathf.Max(0f, transitionDuration),
                null,
                interruptPolicy,
                onStarted,
                onCompleted);
        }

        public bool PlayOneShot(
            string stateName,
            string returnState = null,
            int layer = 0,
            float transitionDuration = 0.1f,
            bool holdFinalFrame = false,
            AnimatorInterruptPolicy interruptPolicy = AnimatorInterruptPolicy.Force,
            Action<string> onStarted = null,
            Action<string> onCompleted = null)
        {
            return StartPlayback(
                stateName,
                layer,
                false,
                holdFinalFrame,
                0f,
                Mathf.Max(0f, transitionDuration),
                returnState,
                interruptPolicy,
                onStarted,
                onCompleted);
        }

        public void Stop(
            bool restorePreviousState = false,
            bool returnToDefaultState = false)
        {
            ClearPlaybackState();

            if (returnToDefaultState)
            {
                TryPlayState(defaultStateName, defaultLayer, 0f, 0f);
                return;
            }

            if (restorePreviousState && hasPreviousState && animator != null)
            {
                animator.Play(previousStateHash, previousLayer, previousNormalizedTime);
            }
        }

        public bool IsPlaying(string stateName = null)
        {
            return isPlaying
                && (string.IsNullOrEmpty(stateName)
                    || string.Equals(activeStateName, stateName, StringComparison.Ordinal));
        }

        #endregion

        #region Playback

        private bool StartPlayback(
            string stateName,
            int layer,
            bool loop,
            bool holdFinalFrame,
            float normalizedTime,
            float transitionDuration,
            string nextStateName,
            AnimatorInterruptPolicy interruptPolicy,
            Action<string> onStarted,
            Action<string> onCompleted)
        {
            if (animator == null
                || string.IsNullOrWhiteSpace(stateName)
                || !IsValidLayer(layer))
            {
                return false;
            }

            string fullStateName = ResolveStateName(stateName, layer);
            int stateHash = Animator.StringToHash(fullStateName);
            if (!animator.HasState(layer, stateHash))
            {
                return false;
            }

            if (isPlaying || holdingFinalFrame)
            {
                if (interruptPolicy == AnimatorInterruptPolicy.Ignore)
                {
                    return false;
                }

                if (interruptPolicy == AnimatorInterruptPolicy.Restart
                    && string.Equals(activeStateName, stateName, StringComparison.Ordinal))
                {
                    normalizedTime = 0f;
                }

                ClearPlaybackState();
            }

            CapturePreviousState(layer);
            activeStateName = stateName;
            returnStateName = nextStateName;
            startedCallback = onStarted;
            completedCallback = onCompleted;
            activeStateHash = stateHash;
            activeLayer = layer;
            activeLoop = loop;
            activeHoldFinalFrame = holdFinalFrame;
            completionRaised = false;
            holdingFinalFrame = false;
            isPlaying = true;

            if (transitionDuration > 0f)
            {
                animator.CrossFade(stateHash, transitionDuration, layer, normalizedTime);
            }
            else
            {
                animator.Play(stateHash, layer, normalizedTime);
            }

            startedCallback?.Invoke(activeStateName);
            return true;
        }

        private void CompleteAnimation()
        {
            if (completionRaised)
            {
                return;
            }

            completionRaised = true;
            isPlaying = false;
            string completedState = activeStateName;
            Action<string> callback = completedCallback;

            if (activeHoldFinalFrame)
            {
                holdingFinalFrame = true;
                callback?.Invoke(completedState);
                return;
            }

            string nextState = returnStateName;
            int nextLayer = activeLayer;
            ClearPlaybackState();
            if (!string.IsNullOrEmpty(nextState))
            {
                TryPlayState(nextState, nextLayer, 0f, 0f);
            }

            callback?.Invoke(completedState);
        }

        #endregion

        #region Helpers

        private void CapturePreviousState(int layer)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            previousStateHash = stateInfo.fullPathHash;
            previousLayer = layer;
            previousNormalizedTime = stateInfo.normalizedTime;
            hasPreviousState = true;
        }

        private bool TryPlayState(
            string stateName,
            int layer,
            float normalizedTime,
            float transitionDuration)
        {
            if (string.IsNullOrWhiteSpace(stateName)
                || animator == null
                || !IsValidLayer(layer))
            {
                return false;
            }

            string fullStateName = ResolveStateName(stateName, layer);
            int stateHash = Animator.StringToHash(fullStateName);
            if (!animator.HasState(layer, stateHash))
            {
                return false;
            }

            if (transitionDuration > 0f)
            {
                animator.CrossFade(stateHash, transitionDuration, layer, normalizedTime);
            }
            else
            {
                animator.Play(stateHash, layer, normalizedTime);
            }

            return true;
        }

        private string ResolveStateName(string stateName, int layer)
        {
            return stateName.IndexOf('.') >= 0
                ? stateName
                : string.Concat(animator.GetLayerName(layer), ".", stateName);
        }

        private bool IsValidLayer(int layer)
        {
            return layer >= 0 && layer < animator.layerCount;
        }

        private void ClearPlaybackState()
        {
            isPlaying = false;
            holdingFinalFrame = false;
            completionRaised = false;
            startedCallback = null;
            completedCallback = null;
            activeStateName = null;
            returnStateName = null;
            activeStateHash = 0;
            activeLayer = 0;
            activeLoop = false;
            activeHoldFinalFrame = false;
        }

        #endregion
    }
}
