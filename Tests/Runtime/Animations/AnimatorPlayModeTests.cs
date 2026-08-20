using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HP.Framework.Animations;
using HP.Framework.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HP.Framework.Tests.Runtime.Animations
{
    public sealed class AnimatorPlayModeTests
    {
        private const string ControllerResourcePath =
            "AnimatorPlay/AnimatorPlayTest";
        private const float CompletionTimeout = 3f;

        private readonly List<GameObject> playbackObjects = new List<GameObject>();
        private GameObject rootObject;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            CreateRoot("AnimatorPlayTestRoot");
            yield return null;

            Assert.That(FrameworkTickRegistry.HasActiveDriver, Is.True);
            Assert.That(FrameworkTickRegistry.ActiveCount, Is.Zero);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = 0; i < playbackObjects.Count; i++)
            {
                if (playbackObjects[i] != null)
                {
                    UnityEngine.Object.Destroy(playbackObjects[i]);
                }
            }

            playbackObjects.Clear();
            if (rootObject != null)
            {
                UnityEngine.Object.Destroy(rootObject);
                rootObject = null;
            }

            yield return null;
            Assert.That(FrameworkTickRegistry.HasActiveDriver, Is.False);
            Assert.That(FrameworkTickRegistry.ActiveCount, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DynamicOneShot_CompletesOnce_AndUnregisters()
        {
            AnimatorPlay playback = CreatePlayback("DynamicOneShot");
            int completionCount = 0;

            Assert.That(
                playback.PlayOneShot(
                    "Inspect",
                    transitionDuration: 0f,
                    onCompleted: _ => completionCount++),
                Is.True);
            Assert.That(FrameworkTickRegistry.ActiveCount, Is.EqualTo(1));

            yield return WaitUntil(() => completionCount == 1);

            Assert.That(playback.IsPlaying(), Is.False);
            Assert.That(FrameworkTickRegistry.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MarkerPlayback_FiresMarkerOnceBeforeCompletion()
        {
            AnimatorPlay playback = CreatePlayback("MarkerPlayback");
            int markerCount = 0;
            int completionCount = 0;

            Assert.That(
                playback.PlayOneShotWithMarker(
                    "Inspect",
                    "Idle",
                    0.5f,
                    _ => markerCount++,
                    transitionDuration: 0f,
                    onCompleted: _ => completionCount++),
                Is.True);

            yield return WaitUntil(() => completionCount == 1);

            Assert.That(markerCount, Is.EqualTo(1));
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(FrameworkTickRegistry.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LoopPlayback_DoesNotRegisterPerObjectTick()
        {
            AnimatorPlay playback = CreatePlayback("LoopPlayback");

            Assert.That(playback.Play("Idle", loop: true), Is.True);
            yield return null;

            Assert.That(playback.IsPlaying("Idle"), Is.True);
            Assert.That(FrameworkTickRegistry.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator HoldFinalFrame_RemainsRegistered_UntilStopped()
        {
            AnimatorPlay playback = CreatePlayback("HoldFinalFrame");
            int completionCount = 0;

            Assert.That(
                playback.PlayOneShot(
                    "Inspect",
                    transitionDuration: 0f,
                    holdFinalFrame: true,
                    onCompleted: _ => completionCount++),
                Is.True);

            yield return WaitUntil(() => completionCount == 1);

            Assert.That(playback.IsPlaying(), Is.False);
            Assert.That(playback.CurrentStateName, Is.EqualTo("Inspect"));
            Assert.That(FrameworkTickRegistry.ActiveCount, Is.EqualTo(1));

            playback.Stop();
            Assert.That(FrameworkTickRegistry.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DisableAndDestroy_RemoveActivePlayback()
        {
            AnimatorPlay playback = CreatePlayback("LifecycleCleanup");
            GameObject playbackObject = playback.gameObject;

            Assert.That(
                playback.PlayOneShot("Inspect", transitionDuration: 0f),
                Is.True);
            Assert.That(FrameworkTickRegistry.ActiveCount, Is.EqualTo(1));

            playbackObject.SetActive(false);
            Assert.That(FrameworkTickRegistry.ActiveCount, Is.Zero);

            playbackObject.SetActive(true);
            Assert.That(
                playback.PlayOneShot("Inspect", transitionDuration: 0f),
                Is.True);
            Assert.That(FrameworkTickRegistry.ActiveCount, Is.EqualTo(1));

            UnityEngine.Object.Destroy(playbackObject);
            yield return null;

            Assert.That(FrameworkTickRegistry.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator StopThenPlaySameFrame_UsesSingleRegistration()
        {
            AnimatorPlay playback = CreatePlayback("StopThenPlay");
            int completionCount = 0;

            Assert.That(
                playback.PlayOneShot("Inspect", transitionDuration: 0f),
                Is.True);
            playback.Stop();
            Assert.That(
                playback.PlayOneShot(
                    "Inspect",
                    transitionDuration: 0f,
                    onCompleted: _ => completionCount++),
                Is.True);

            Assert.That(FrameworkTickRegistry.ActiveCount, Is.EqualTo(1));
            yield return WaitUntil(() => completionCount == 1);
            Assert.That(FrameworkTickRegistry.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CompletionCallback_CanStartNextPlayback_WithoutDuplicateTick()
        {
            AnimatorPlay playback = CreatePlayback("CallbackRestart");
            int completionCount = 0;
            Action<string> onCompleted = null;
            onCompleted = _ =>
            {
                completionCount++;
                if (completionCount == 1)
                {
                    Assert.That(
                        playback.PlayOneShot(
                            "Inspect",
                            transitionDuration: 0f,
                            onCompleted: onCompleted),
                        Is.True);
                    Assert.That(FrameworkTickRegistry.ActiveCount, Is.EqualTo(1));
                }
            };

            Assert.That(
                playback.PlayOneShot(
                    "Inspect",
                    transitionDuration: 0f,
                    onCompleted: onCompleted),
                Is.True);

            yield return WaitUntil(() => completionCount == 2);

            Assert.That(FrameworkTickRegistry.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator UnregisterDuringDispatch_SkipsRemovedPlayback()
        {
            AnimatorPlay first = CreatePlayback("FirstPlayback");
            AnimatorPlay second = CreatePlayback("SecondPlayback");
            int firstCompletionCount = 0;
            int secondCompletionCount = 0;

            Assert.That(
                first.PlayOneShot(
                    "Inspect",
                    transitionDuration: 0f,
                    onCompleted: _ =>
                    {
                        firstCompletionCount++;
                        second.Stop();
                    }),
                Is.True);
            Assert.That(
                second.PlayOneShot(
                    "Inspect",
                    transitionDuration: 0f,
                    onCompleted: _ => secondCompletionCount++),
                Is.True);
            Assert.That(FrameworkTickRegistry.ActiveCount, Is.EqualTo(2));

            yield return WaitUntil(() => firstCompletionCount == 1);
            yield return null;

            Assert.That(secondCompletionCount, Is.Zero);
            Assert.That(FrameworkTickRegistry.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MissingRoot_RejectsPlaybackWithoutChangingState()
        {
            UnityEngine.Object.Destroy(rootObject);
            rootObject = null;
            yield return null;

            AnimatorPlay playback = CreatePlayback("MissingRoot");
            LogAssert.Expect(
                LogType.Error,
                new Regex("AnimatorPlay.*RootLifetimeScope tick driver"));

            Assert.That(
                playback.PlayOneShot("Inspect", transitionDuration: 0f),
                Is.False);
            Assert.That(playback.IsPlaying(), Is.False);
            Assert.That(playback.CurrentStateName, Is.Null);
            Assert.That(FrameworkTickRegistry.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DuplicateRoot_DoesNotDoubleDispatch()
        {
            LogAssert.Expect(
                LogType.Error,
                new Regex("Multiple framework tick drivers are active"));
            GameObject duplicateRoot = CreateRoot("DuplicateAnimatorPlayTestRoot");
            int completionCount = 0;
            AnimatorPlay playback = CreatePlayback("DuplicateRootPlayback");

            Assert.That(
                playback.PlayOneShot(
                    "Inspect",
                    transitionDuration: 0f,
                    onCompleted: _ => completionCount++),
                Is.True);

            yield return WaitUntil(() => completionCount == 1);
            yield return null;

            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(FrameworkTickRegistry.ActiveCount, Is.Zero);
            UnityEngine.Object.Destroy(duplicateRoot);
        }

        [UnityTest]
        public IEnumerator TickException_RemovesFaultingItem_AndContinuesDispatch()
        {
            ThrowingTickable throwing = new ThrowingTickable();
            CountingTickable counting = new CountingTickable();
            FrameworkTickRegistry.Register(throwing);
            FrameworkTickRegistry.Register(counting);
            LogAssert.Expect(
                LogType.Exception,
                new Regex("Framework tick test exception"));

            yield return null;

            Assert.That(counting.TickCount, Is.EqualTo(1));
            Assert.That(FrameworkTickRegistry.ActiveCount, Is.EqualTo(1));
            FrameworkTickRegistry.Unregister(counting);
        }

        private GameObject CreateRoot(string name)
        {
            GameObject instance = new GameObject(name);
            instance.SetActive(false);
            RootLifetimeScope scope = instance.AddComponent<RootLifetimeScope>();
            scope.autoRun = false;
            instance.SetActive(true);
            scope.Build();

            if (rootObject == null)
            {
                rootObject = instance;
            }

            return instance;
        }

        private AnimatorPlay CreatePlayback(string name)
        {
            RuntimeAnimatorController controller =
                Resources.Load<RuntimeAnimatorController>(ControllerResourcePath);
            Assert.That(controller, Is.Not.Null);

            GameObject instance = new GameObject(name);
            playbackObjects.Add(instance);
            Animator animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0f);
            return instance.AddComponent<AnimatorPlay>();
        }

        private static IEnumerator WaitUntil(Func<bool> predicate)
        {
            float deadline = Time.realtimeSinceStartup + CompletionTimeout;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(predicate(), Is.True, "Timed out waiting for playback state.");
        }

        private sealed class ThrowingTickable : IFrameworkTickable
        {
            public void Tick()
            {
                throw new InvalidOperationException("Framework tick test exception.");
            }
        }

        private sealed class CountingTickable : IFrameworkTickable
        {
            public int TickCount { get; private set; }

            public void Tick()
            {
                TickCount++;
            }
        }
    }
}
