using System;
using System.Collections.Generic;
using HP.Framework.Lifecycle;
using NUnit.Framework;
using UnityEngine;

namespace HP.Framework.Tests
{
    public sealed class ApplicationLifecycleTests
    {
        private GameObject root;
        private ApplicationLifecycleService lifecycle;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ApplicationLifecycleTests");
            lifecycle = root.AddComponent<ApplicationLifecycleService>();
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PauseResume_AreIdempotentAndOrdered()
        {
            var events = new List<string>();
            lifecycle.Paused += () => events.Add("Paused");
            lifecycle.Resumed += () => events.Add("Resumed");

            Assert.That(lifecycle.SetPaused(true), Is.True);
            Assert.That(lifecycle.SetPaused(true), Is.False);
            Assert.That(lifecycle.SetPaused(false), Is.True);
            Assert.That(lifecycle.SetPaused(false), Is.False);

            CollectionAssert.AreEqual(new[] { "Paused", "Resumed" }, events);
        }

        [Test]
        public void Focus_IsIndependentFromPauseAndOnlyFiresOnRealChange()
        {
            bool targetFocus = !lifecycle.HasFocus;
            int focusEvents = 0;
            bool observed = lifecycle.HasFocus;
            lifecycle.FocusChanged += value =>
            {
                focusEvents++;
                observed = value;
            };

            Assert.That(lifecycle.SetFocus(targetFocus), Is.True);
            Assert.That(lifecycle.SetFocus(targetFocus), Is.False);

            Assert.That(focusEvents, Is.EqualTo(1));
            Assert.That(observed, Is.EqualTo(targetFocus));
            Assert.That(lifecycle.IsPaused, Is.False);
        }

        [Test]
        public void LowMemory_DispatchesEverySignal()
        {
            int count = 0;
            lifecycle.LowMemory += () => count++;

            lifecycle.SignalLowMemory();
            lifecycle.SignalLowMemory();

            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public void Quitting_DispatchesOnce()
        {
            int count = 0;
            lifecycle.Quitting += () => count++;

            Assert.That(lifecycle.SignalQuitting(), Is.True);
            Assert.That(lifecycle.SignalQuitting(), Is.False);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(lifecycle.IsQuitting, Is.True);
        }

        [Test]
        public void Dispose_SilencesFurtherDispatch()
        {
            int count = 0;
            lifecycle.LowMemory += () => count++;

            lifecycle.Dispose();
            lifecycle.SignalLowMemory();

            Assert.That(count, Is.Zero);
        }
    }
}
