using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Base.Bootstrap;
using Base.Common;

namespace Base.Tests
{
    public sealed class ProcedureManagerTests
    {
        private sealed class TrackingProcedure : Procedure
        {
            public int EnterCount { get; private set; }
            public int ExitCount { get; private set; }

            public override void OnEnter() => EnterCount++;
            public override void OnExit() => ExitCount++;
        }

        private sealed class SecondProcedure : Procedure
        {
            public int EnterCount { get; private set; }
            public override void OnEnter() => EnterCount++;
        }

        private sealed class ThrowingProcedure : Procedure
        {
            public override void OnEnter()
            {
                throw new InvalidOperationException("Expected test failure.");
            }
        }

        [Test]
        public void ChangeState_UsesOnlyExplicitlyRegisteredProcedures()
        {
            var first = new TrackingProcedure();
            var second = new SecondProcedure();
            using var manager = new ProcedureManager(new Procedure[] { first, second });

            manager.ChangeState<TrackingProcedure>();
            manager.ChangeState<SecondProcedure>();

            Assert.That(first.EnterCount, Is.EqualTo(1));
            Assert.That(first.ExitCount, Is.EqualTo(1));
            Assert.That(second.EnterCount, Is.EqualTo(1));
            Assert.That(manager.CurrentProcedure, Is.SameAs(second));
            Assert.That(manager.TransitionState, Is.EqualTo(ProcedureTransitionState.Idle));
        }

        [Test]
        public void ChangeState_UnregisteredProcedure_ThrowsClearError()
        {
            using var manager = new ProcedureManager(Array.Empty<Procedure>());
            Assert.Throws<InvalidOperationException>(() => manager.ChangeState<TrackingProcedure>());
        }

        [Test]
        public void ChangeState_FailedEnter_RestoresPreviousProcedure()
        {
            var stable = new TrackingProcedure();
            var failing = new ThrowingProcedure();
            using var manager = new ProcedureManager(new Procedure[] { stable, failing });

            manager.ChangeState<TrackingProcedure>();
            Assert.Throws<InvalidOperationException>(() => manager.ChangeState<ThrowingProcedure>());

            Assert.That(manager.CurrentProcedure, Is.SameAs(stable));
            Assert.That(manager.TransitionState, Is.EqualTo(ProcedureTransitionState.Failed));
            Assert.That(manager.LastTransitionException, Is.TypeOf<InvalidOperationException>());
        }
    }

    public sealed class UtilityContractTests
    {
        [Test]
        public void ObjectArrayTryGet_ReturnsFalseWhenConversionFails()
        {
            object[] values = { "not-an-int" };
            Assert.That(values.TryGet<int>(0, out _), Is.False);
        }

        [Test]
        public void ObjectArrayTryGet_UsesInvariantVectorParsing()
        {
            object[] values = { "(1.5, 2.25, -3)" };
            Assert.That(values.TryGet<Vector3>(0, out Vector3 result), Is.True);
            Assert.That(result, Is.EqualTo(new Vector3(1.5f, 2.25f, -3f)));
        }

        [Test]
        public void CurveHelpers_HandleZeroAndSingleSegment()
        {
            Assert.That(Base.Common.MathUtils.GetBezierCurve(Vector3.zero, Vector3.one, Vector3.one, Vector3.one, 0), Is.Empty);
            Vector3[] single = Base.Common.MathUtils.GetBezierCurve(Vector3.zero, Vector3.one, Vector3.one, Vector3.one, 1);
            Assert.That(single, Has.Length.EqualTo(1));
            Assert.That(single[0], Is.EqualTo(Vector3.zero));
        }
    }

    public sealed class BootstrapPrefabTests
    {
        private const string BootstrapPath = "Packages/com.base.vcontainer/Prefabs/Bootstrap.prefab";

        [Test]
        public void BootstrapPrefab_HasNoMissingScripts_AndContainsRequiredManagers()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapPath);
            Assert.That(prefab, Is.Not.Null, $"Bootstrap prefab was not found at {BootstrapPath}.");

            GameObject root = UnityEditor.PrefabUtility.LoadPrefabContents(BootstrapPath);
            try
            {
                int missingScripts = 0;
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
                }

                Assert.That(missingScripts, Is.Zero);
                Assert.That(root.GetComponent<RootLifetimeScope>(), Is.Not.Null);
                Assert.That(root.GetComponent<AudioManager>(), Is.Not.Null);
                Assert.That(root.GetComponent<UIManager>(), Is.Not.Null);
                Assert.That(root.GetComponent<InputManager>(), Is.Not.Null);
                Assert.That(root.GetComponent<GameSceneManager>(), Is.Not.Null);
                Assert.That(root.GetComponent<PoolManager>(), Is.Not.Null);
                Assert.That(root.GetComponent<HapticManager>(), Is.Not.Null);
            }
            finally
            {
                UnityEditor.PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}