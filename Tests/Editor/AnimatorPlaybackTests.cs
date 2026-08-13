using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using HP.Framework.Animations;

namespace HP.Framework.Tests
{
    public sealed class AnimatorPlaybackTests
    {
        private GameObject animatorObject;
        private AnimatorPlayback playback;

        [SetUp]
        public void SetUp()
        {
            animatorObject = new GameObject("AnimatorPlaybackTest");
            Animator animator = animatorObject.AddComponent<Animator>();
            AnimatorController controller = new AnimatorController
            {
                name = "AnimatorPlaybackTestController"
            };
            controller.AddLayer("Base Layer");
            controller.layers[0].stateMachine.AddState("Idle");
            controller.layers[0].stateMachine.AddState("Inspect");
            animator.runtimeAnimatorController = controller;
            playback = animatorObject.AddComponent<AnimatorPlayback>();
        }

        [TearDown]
        public void TearDown()
        {
            if (animatorObject != null)
            {
                Object.DestroyImmediate(animatorObject);
            }
        }

        [Test]
        public void Play_ResolvesShortStateName()
        {
            Assert.That(playback.Play("Inspect"), Is.True);
            Assert.That(playback.IsPlaying("Inspect"), Is.True);
            Assert.That(playback.CurrentStateName, Is.EqualTo("Inspect"));
        }

        [Test]
        public void CrossFade_ResolvesState()
        {
            Assert.That(playback.CrossFade("Inspect", 0.15f), Is.True);
            Assert.That(playback.CurrentStateName, Is.EqualTo("Inspect"));
        }

        [Test]
        public void PlayOneShot_AcceptsRuntimeCallbacks()
        {
            string startedState = null;
            string completedState = null;

            Assert.That(
                playback.PlayOneShot(
                    "Inspect",
                    "Idle",
                    onStarted: stateName => startedState = stateName,
                    onCompleted: stateName => completedState = stateName),
                Is.True);

            Assert.That(startedState, Is.EqualTo("Inspect"));
            Assert.That(completedState, Is.Null);
        }

        [Test]
        public void InvalidState_IsRejectedWithoutStartingPlayback()
        {
            Assert.That(playback.Play("MissingState"), Is.False);
            Assert.That(playback.IsPlaying(), Is.False);
        }

        [Test]
        public void Ignore_DoesNotReplaceCurrentPlayback()
        {
            Assert.That(playback.Play("Inspect"), Is.True);
            Assert.That(
                playback.Play(
                    "Idle",
                    interruptPolicy: AnimatorInterruptPolicy.Ignore),
                Is.False);
            Assert.That(playback.CurrentStateName, Is.EqualTo("Inspect"));
        }

        [Test]
        public void Stop_ClearsPlaybackState()
        {
            playback.PlayOneShot("Inspect");
            playback.Stop();
            Assert.That(playback.IsPlaying(), Is.False);
            Assert.That(playback.CurrentStateName, Is.Null);
        }
    }
}
