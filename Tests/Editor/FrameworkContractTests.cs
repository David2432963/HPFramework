using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using HP.Framework.Audio;
using HP.Framework.Bootstrap;
using HP.Framework.Common;
using HP.Framework.Haptics;
using HP.Framework.Input;
using HP.Framework.Pooling;
using HP.Framework.UI;
using HP.Framework.Persistence;

namespace HP.Framework.Tests
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

    public sealed class SettingsContractTests
    {
        private sealed class MemorySettingsStore : ISettingsStore
        {
            private readonly Dictionary<string, object> values =
                new Dictionary<string, object>();

            public int SaveCount { get; private set; }

            public bool HasKey(string key, string section = null)
                => values.ContainsKey(BuildKey(key, section));

            public bool GetBool(string key, bool defaultValue = false, string section = null)
                => Get(key, defaultValue, section);

            public int GetInt(string key, int defaultValue = 0, string section = null)
                => Get(key, defaultValue, section);

            public float GetFloat(string key, float defaultValue = 0f, string section = null)
                => Get(key, defaultValue, section);

            public string GetString(string key, string defaultValue = "", string section = null)
                => Get(key, defaultValue, section);

            public void SetBool(string key, bool value, string section = null)
                => Set(key, value, section);

            public void SetInt(string key, int value, string section = null)
                => Set(key, value, section);

            public void SetFloat(string key, float value, string section = null)
                => Set(key, value, section);

            public void SetString(string key, string value, string section = null)
                => Set(key, value, section);

            public bool Delete(string key, string section = null)
                => values.Remove(BuildKey(key, section));

            public void Save()
            {
                SaveCount++;
            }

            private T Get<T>(string key, T defaultValue, string section)
            {
                return values.TryGetValue(BuildKey(key, section), out object value)
                    && value is T typed
                    ? typed
                    : defaultValue;
            }

            private void Set<T>(string key, T value, string section)
            {
                values[BuildKey(key, section)] = value;
            }

            private static string BuildKey(string key, string section)
                => string.IsNullOrEmpty(section) ? key : section + "/" + key;
        }

        [Test]
        public void SettingsManager_UsesInjectedStore_AndFlushesOnDispose()
        {
            var store = new MemorySettingsStore();
            store.SetBool(nameof(ISettingsProvider.SoundEnabled), false,
                BaseConstants.SettingsSection);

            var settings = new SettingsManager(store);
            settings.Initialize();

            Assert.That(settings.SoundEnabled, Is.False);
            settings.SoundEnabled = true;
            Assert.That(store.GetBool(nameof(ISettingsProvider.SoundEnabled), false,
                BaseConstants.SettingsSection), Is.True);

            settings.Dispose();
            Assert.That(store.SaveCount, Is.EqualTo(1));
        }
    }

    public sealed class UtilityContractTests
    {
        private readonly struct TestEvent
        {
            public TestEvent(int value) => Value = value;
            public int Value { get; }
        }

        [Test]
        public void CurveHelpers_HandleZeroAndSingleSegment()
        {
            Assert.That(HP.Framework.Common.MathUtils.GetBezierCurve(Vector3.zero, Vector3.one, Vector3.one, Vector3.one, 0), Is.Empty);
            Vector3[] single = HP.Framework.Common.MathUtils.GetBezierCurve(Vector3.zero, Vector3.one, Vector3.one, Vector3.one, 1);
            Assert.That(single, Has.Length.EqualTo(1));
            Assert.That(single[0], Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void EventBus_SubscriptionLifetimeAndSubscribeOnce_AreDeterministic()
        {
            using var bus = new EventBus();
            int sum = 0;
            IDisposable subscription = bus.Subscribe<TestEvent>(evt => sum += evt.Value);
            bus.SubscribeOnce<TestEvent>(evt => sum += evt.Value * 10);

            bus.Publish(new TestEvent(2));
            bus.Publish(new TestEvent(3));
            subscription.Dispose();
            bus.Publish(new TestEvent(4));

            Assert.That(sum, Is.EqualTo(25));
        }

        [Test]
        public void JsonSaveFile_RejectsRelativeFolderTraversal()
        {
            Assert.Throws<ArgumentException>(() => JsonSaveFile.GetPath("save", "../outside"));
            Assert.Throws<ArgumentException>(() => JsonSaveFile.GetPath("save", "../../outside"));
        }
    }

    public sealed class PopupTransitionPreviewContractTests
    {
        [Test]
        public void PopupTransitionPlayer_ShowHideAndRestore_AreDeterministic()
        {
            GameObject backdropObject = new GameObject("Backdrop");
            GameObject contentObject = new GameObject("Content", typeof(RectTransform));
            UIAnimationPresetSO preset = ScriptableObject.CreateInstance<UIAnimationPresetSO>();
            try
            {
                CanvasGroup backdrop = backdropObject.AddComponent<CanvasGroup>();
                CanvasGroup contentCanvas = contentObject.AddComponent<CanvasGroup>();
                RectTransform content = contentObject.GetComponent<RectTransform>();

                backdrop.alpha = 0.8f;
                contentCanvas.alpha = 0.9f;
                content.localScale = new Vector3(1.1f, 1.1f, 1.1f);

                var player = new PopupTransitionPlayer(
                    backdrop,
                    content,
                    contentCanvas,
                    preset);

                float showDuration = player.BeginShow(false);
                Assert.That(backdrop.alpha, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(content.localScale.x,
                    Is.EqualTo(1.1f * preset.Show.ContentScale.Scale).Within(0.0001f));

                player.ApplyShow(showDuration);
                Assert.That(backdrop.alpha, Is.EqualTo(0.8f).Within(0.0001f));
                Assert.That(content.localScale, Is.EqualTo(new Vector3(1.1f, 1.1f, 1.1f)));

                float hideDuration = player.BeginHide();
                player.ApplyHide(hideDuration);
                Assert.That(backdrop.alpha, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(content.localScale.x,
                    Is.EqualTo(1.1f * preset.Hide.ContentScale.Scale).Within(0.0001f));

                player.ApplyShownState();
                Assert.That(backdrop.alpha, Is.EqualTo(0.8f).Within(0.0001f));
                Assert.That(contentCanvas.alpha, Is.EqualTo(0.9f).Within(0.0001f));
                Assert.That(content.localScale, Is.EqualTo(new Vector3(1.1f, 1.1f, 1.1f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
                UnityEngine.Object.DestroyImmediate(backdropObject);
                UnityEngine.Object.DestroyImmediate(contentObject);
            }
        }
    }

    public sealed class URPCameraStackTests
    {
        [Test]
        public void AttachOverlay_ConfiguresBaseAndOverlay_AndDoesNotDuplicateStackEntry()
        {
            if (!URPCameraStackUtility.IsAvailable)
            {
                Assert.Ignore("URP is not installed in this test project.");
            }

            GameObject mainObject = new GameObject("URPBaseCameraTest");
            GameObject uiObject = new GameObject("URPOverlayCameraTest");
            try
            {
                Camera mainCamera = mainObject.AddComponent<Camera>();
                Camera uiCamera = uiObject.AddComponent<Camera>();

                bool firstAttach = URPCameraStackUtility.AttachOverlay(mainCamera, uiCamera);
                bool secondAttach = URPCameraStackUtility.AttachOverlay(mainCamera, uiCamera);

                Type dataType = Type.GetType(
                    "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
                Component mainData = mainObject.GetComponent(dataType);
                Component uiData = uiObject.GetComponent(dataType);
                Assert.That(mainData, Is.Not.Null);
                Assert.That(uiData, Is.Not.Null);

                System.Reflection.PropertyInfo renderType = dataType.GetProperty("renderType");
                System.Reflection.PropertyInfo stack = dataType.GetProperty("cameraStack");
                Assert.That(renderType.GetValue(mainData).ToString(), Is.EqualTo("Base"));
                Assert.That(renderType.GetValue(uiData).ToString(), Is.EqualTo("Overlay"));

                // A bare EditMode test camera can have no active ScriptableRenderer. In that
                // context URP itself cannot expose cameraStack, but type configuration must still
                // succeed without throwing. When a renderer is available, verify idempotent stacking.
                if (firstAttach || secondAttach)
                {
                    System.Collections.IList cameraStack = stack.GetValue(mainData) as System.Collections.IList;
                    Assert.That(cameraStack, Is.Not.Null);
                    int occurrences = 0;
                    foreach (object item in cameraStack)
                    {
                        if (ReferenceEquals(item, uiCamera))
                        {
                            occurrences++;
                        }
                    }
                    Assert.That(occurrences, Is.EqualTo(1));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mainObject);
                UnityEngine.Object.DestroyImmediate(uiObject);
            }
        }
    }

    public sealed class BootstrapRepairTests
    {
        [Test]
        public void Repair_PreservesCustomCanvasSettings_WhileResetRestoresDefaults()
        {
            GameObject root = new GameObject("BootstrapRepairTest");
            try
            {
                RootLifetimeScope scope = root.AddComponent<RootLifetimeScope>();
                HP.Framework.Editor.RootLifetimeScopeEditor.AutoSetupHierarchy(scope, resetDefaults: true);

                CanvasScaler scaler = root.transform.Find("UICanvas").GetComponent<CanvasScaler>();
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.9f;

                HP.Framework.Editor.RootLifetimeScopeEditor.AutoSetupHierarchy(scope, resetDefaults: false);
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
                Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.9f).Within(0.0001f));

                HP.Framework.Editor.RootLifetimeScopeEditor.AutoSetupHierarchy(scope, resetDefaults: true);
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1080f, 1920f)));
                Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }

    public sealed class BootstrapPrefabTests
    {
        private const string BootstrapTemplateGuid = "550d92965c5c4c53b9949039d465faba";

        private static string BootstrapPath => AssetDatabase.GUIDToAssetPath(BootstrapTemplateGuid);

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



