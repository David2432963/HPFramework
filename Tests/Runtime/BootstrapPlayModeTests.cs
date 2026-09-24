using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;
using HP.Framework.Assets;
using HP.Framework.Audio;
using HP.Framework.Bootstrap;
using HP.Framework.Bootstrap.Loading;
using HP.Framework.Common;
using HP.Framework.Haptics;
using HP.Framework.Input;
using HP.Framework.Lifecycle;
using HP.Framework.Persistence;
using HP.Framework.Pooling;
using HP.Framework.Startup;
using HP.Framework.UI;

namespace HP.Framework.Tests
{
    public sealed class StartupProbeTaskA : IStartupTask
    {
        public string Id => "probe-a";
        public StartupTaskCriticality Criticality => StartupTaskCriticality.Required;
        public int Attempts { get; private set; }

        public UniTask ExecuteAsync(IProgress<float> progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Attempts++;
            progress?.Report(1f);
            return UniTask.CompletedTask;
        }
    }

    public sealed class StartupProbeTaskB : IStartupTask
    {
        public string Id => "probe-b";
        public StartupTaskCriticality Criticality => StartupTaskCriticality.Required;
        public int Attempts { get; private set; }

        public UniTask ExecuteAsync(IProgress<float> progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Attempts++;
            progress?.Report(1f);
            return UniTask.CompletedTask;
        }
    }

    public sealed class StartupProbeRootLifetimeScope : RootLifetimeScope
    {
        protected override void RegisterApplicationStartupTasks(IContainerBuilder builder)
        {
            builder.RegisterStartupTask<StartupProbeTaskA>(0, TimeSpan.Zero);
            builder.RegisterStartupTask<StartupProbeTaskB>(0, TimeSpan.Zero);
        }
    }

    public sealed class ScopeTestDependency : System.IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    public sealed class ScopedPoolInjectionProbe : MonoBehaviour
    {
        public ScopeTestDependency Dependency { get; private set; }

        [Inject]
        public void Construct(ScopeTestDependency dependency)
        {
            Dependency = dependency;
        }
    }

    public sealed class ScopedPopupInjectionProbe : BasePopup
    {
        public ScopeTestDependency Dependency { get; private set; }
        public IUIService InjectedUIService => uiService;

        [Inject]
        public void ConstructScoped(ScopeTestDependency dependency)
        {
            Dependency = dependency;
        }
    }

    public sealed class OwnedPopupProbe : BasePopup
    {
    }

    public sealed class OwnedScreenProbe : BaseScreen
    {
    }

    public sealed class ScopeArchitecturePlayModeTests
    {
        public sealed class ScopedMarker : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        [UnityTest]
        public IEnumerator RootSceneFeatureScopes_InheritDependencies_AndDisposeSceneOwnedState()
        {
            GameObject rootObject = new GameObject("ScopeArchitectureTest");
            rootObject.SetActive(false);
            RootLifetimeScope rootScope = rootObject.AddComponent<RootLifetimeScope>();
            rootScope.autoRun = false;
            rootObject.SetActive(true);
            rootScope.Build();

            ISettingsService rootSettings = rootScope.Container.Resolve<ISettingsService>();
            IEventBus rootEventBus = rootScope.Container.Resolve<IEventBus>();

            BaseSceneLifetimeScope sceneScope = rootScope.CreateChild<BaseSceneLifetimeScope>(
                builder => builder.Register<ScopedMarker>(Lifetime.Singleton).AsSelf(),
                "SceneScope");
            ScopedMarker sceneMarker = sceneScope.Container.Resolve<ScopedMarker>();

            BaseFeatureLifetimeScope featureScope = sceneScope.CreateChild<BaseFeatureLifetimeScope>(
                builder => { },
                "FeatureScope");

            Assert.That(sceneScope.Parent, Is.SameAs(rootScope));
            Assert.That(featureScope.Parent, Is.SameAs(sceneScope));
            Assert.That(featureScope.Container.Resolve<ISettingsService>(), Is.SameAs(rootSettings));
            Assert.That(featureScope.Container.Resolve<IEventBus>(), Is.SameAs(rootEventBus));
            Assert.That(featureScope.Container.Resolve<ScopedMarker>(), Is.SameAs(sceneMarker));

            UnityEngine.Object.Destroy(sceneScope.gameObject);
            yield return null;
            Assert.That(sceneMarker.IsDisposed, Is.True);

            UnityEngine.Object.Destroy(rootObject);
            yield return null;
        }
    }

    public sealed class BootstrapPlayModeTests
    {
        [Test]
        public void SceneLoadStartedException_ReportsFailureAndResetsLoadingState()
        {
            GameObject managerObject = new GameObject("SceneLoadLifecycleTest");
            try
            {
                GameSceneManager sceneManager = managerObject.AddComponent<GameSceneManager>();
                bool failureReported = false;
                sceneManager.SceneLoadStarted += _ =>
                    throw new InvalidOperationException("SceneLoadStarted test failure.");
                sceneManager.SceneLoadFailed += (_, exception) =>
                    failureReported = exception is InvalidOperationException;

                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await sceneManager.LoadSceneAsync(BaseConstants.DefaultLoadingSceneName));

                Assert.That(failureReported, Is.True);
                Assert.That(sceneManager.IsLoading, Is.False);
                Assert.That(sceneManager.CurrentLoadStage, Is.EqualTo(SceneLoadStage.Idle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [UnityTest]
        public IEnumerator LoadingScreen_InitializesUpdatesAndUnsubscribes()
        {
            GameObject managerObject = new GameObject("LoadingScreenManagerTest");
            GameObject screenObject = new GameObject("LoadingScreenTest");
            GameObject sliderObject = new GameObject(
                "ProgressBar",
                typeof(RectTransform),
                typeof(Slider));
            GameObject textObject = new GameObject(
                "ProgressText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            GameObject imageObject = new GameObject(
                "PresentationImage",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            GameObject continueObject = new GameObject(
                "ContinueButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            Texture2D spriteTexture = new Texture2D(2, 1);
            Sprite loadingSprite = Sprite.Create(
                spriteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f));
            Sprite readySprite = Sprite.Create(
                spriteTexture,
                new Rect(1f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f));

            try
            {
                GameSceneManager sceneManager = managerObject.AddComponent<GameSceneManager>();
                LoadingScreen loadingScreen = screenObject.AddComponent<LoadingScreen>();
                Slider slider = sliderObject.GetComponent<Slider>();
                Text text = textObject.GetComponent<Text>();
                Image presentationImage = imageObject.GetComponent<Image>();
                Button continueButton = continueObject.GetComponent<Button>();
                SetField(loadingScreen, "progressBar", slider);
                SetField(loadingScreen, "progressText", text);
                SetField(loadingScreen, "presentationImage", presentationImage);
                SetField(loadingScreen, "loadingSprite", loadingSprite);
                SetField(loadingScreen, "readySprite", readySprite);
                SetField(loadingScreen, "continueButton", continueButton);

                slider.value = 0.75f;
                text.text = "75%";
                loadingScreen.Construct(sceneManager);

                Assert.That(slider.value, Is.Zero);
                Assert.That(text.text, Is.EqualTo("0%"));
                Assert.That(presentationImage.sprite, Is.SameAs(loadingSprite));
                Assert.That(continueObject.activeSelf, Is.False);

                FieldInfo progressEvent = typeof(GameSceneManager).GetField(
                    "LoadProgressChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo stageEvent = typeof(GameSceneManager).GetField(
                    "LoadStageChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo failureEvent = typeof(GameSceneManager).GetField(
                    "SceneLoadFailed",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(progressEvent, Is.Not.Null);
                Assert.That(stageEvent, Is.Not.Null);
                Assert.That(failureEvent, Is.Not.Null);

                Action<float> progressChanged = progressEvent.GetValue(sceneManager) as Action<float>;
                Action<SceneLoadStage> stageChanged = stageEvent.GetValue(sceneManager) as Action<SceneLoadStage>;
                Action<string, Exception> loadFailed =
                    failureEvent.GetValue(sceneManager) as Action<string, Exception>;
                Assert.That(progressChanged, Is.Not.Null);
                Assert.That(stageChanged, Is.Not.Null);
                Assert.That(loadFailed, Is.Not.Null);

                progressChanged.Invoke(0.42f);
                Assert.That(slider.value, Is.EqualTo(0.42f).Within(0.0001f));
                Assert.That(text.text, Is.EqualTo("42%"));

                stageChanged.Invoke(SceneLoadStage.WaitingForReadiness);
                Assert.That(text.text, Does.Contain("42%"));
                Assert.That(text.text, Does.Contain("Preparing gameplay"));
                Assert.That(presentationImage.sprite, Is.SameAs(loadingSprite));
                Assert.That(continueObject.activeSelf, Is.False);

                stageChanged.Invoke(SceneLoadStage.WaitingForConfirmation);
                Assert.That(text.text, Does.Contain("Ready"));
                Assert.That(presentationImage.sprite, Is.SameAs(readySprite));
                Assert.That(continueObject.activeSelf, Is.True);
                Assert.That(continueButton.interactable, Is.True);

                loadFailed.Invoke("Game", new InvalidOperationException("readiness failed"));
                Assert.That(text.text, Is.EqualTo("Loading failed"));
                Assert.That(presentationImage.sprite, Is.SameAs(loadingSprite));
                Assert.That(continueObject.activeSelf, Is.False);

                UnityEngine.Object.Destroy(screenObject);
                yield return null;
                Assert.That(progressEvent.GetValue(sceneManager), Is.Null);
                Assert.That(stageEvent.GetValue(sceneManager), Is.Null);
                Assert.That(failureEvent.GetValue(sceneManager), Is.Null);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.Destroy(managerObject);
                UnityEngine.Object.Destroy(screenObject);
                UnityEngine.Object.Destroy(sliderObject);
                UnityEngine.Object.Destroy(textObject);
                UnityEngine.Object.Destroy(imageObject);
                UnityEngine.Object.Destroy(continueObject);
                UnityEngine.Object.Destroy(loadingSprite);
                UnityEngine.Object.Destroy(readySprite);
                UnityEngine.Object.Destroy(spriteTexture);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator LoadingScreen_ComponentProgress_UpdatesFillIndicatorAndReadyState()
        {
            GameObject managerObject = new GameObject("LoadingScreenComponentManager");
            GameObject screenObject = new GameObject("LoadingScreenComponentView");
            GameObject loadingRoot = new GameObject("LoadingState");
            GameObject readyRoot = new GameObject("ReadyState");
            loadingRoot.transform.SetParent(screenObject.transform, false);
            readyRoot.transform.SetParent(screenObject.transform, false);

            GameObject fillObject = new GameObject(
                "Fill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            fillObject.transform.SetParent(loadingRoot.transform, false);
            Image fill = fillObject.GetComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;

            GameObject trackObject = new GameObject("Track", typeof(RectTransform));
            trackObject.transform.SetParent(loadingRoot.transform, false);
            RectTransform track = trackObject.GetComponent<RectTransform>();
            track.sizeDelta = new Vector2(100f, 10f);

            GameObject indicatorObject = new GameObject(
                "Indicator",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            indicatorObject.transform.SetParent(track, false);
            RectTransform indicator = indicatorObject.GetComponent<RectTransform>();
            indicator.anchorMin = new Vector2(-0.05f, 0f);
            indicator.anchorMax = new Vector2(0.05f, 1f);
            indicator.offsetMin = Vector2.zero;
            indicator.offsetMax = Vector2.zero;

            GameObject continueObject = new GameObject(
                "Continue",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            continueObject.transform.SetParent(readyRoot.transform, false);

            try
            {
                GameSceneManager sceneManager = managerObject.AddComponent<GameSceneManager>();
                LoadingScreen loadingScreen = screenObject.AddComponent<LoadingScreen>();
                SetField(loadingScreen, "loadingVisualRoot", loadingRoot);
                SetField(loadingScreen, "readyVisualRoot", readyRoot);
                SetField(loadingScreen, "progressFillImage", fill);
                SetField(loadingScreen, "progressTrack", track);
                SetField(loadingScreen, "progressIndicator", indicator);
                SetField(loadingScreen, "continueButton", continueObject.GetComponent<Button>());

                loadingScreen.Construct(sceneManager);

                Assert.That(loadingRoot.activeSelf, Is.True);
                Assert.That(readyRoot.activeSelf, Is.False);
                Assert.That(fill.fillAmount, Is.Zero);
                Assert.That((indicator.anchorMin.x + indicator.anchorMax.x) * 0.5f, Is.Zero.Within(0.0001f));

                FieldInfo progressEvent = typeof(GameSceneManager).GetField(
                    "LoadProgressChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo stageEvent = typeof(GameSceneManager).GetField(
                    "LoadStageChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Action<float> progressChanged = progressEvent.GetValue(sceneManager) as Action<float>;
                Action<SceneLoadStage> stageChanged = stageEvent.GetValue(sceneManager) as Action<SceneLoadStage>;

                progressChanged.Invoke(0.5f);
                Assert.That(fill.fillAmount, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(
                    (indicator.anchorMin.x + indicator.anchorMax.x) * 0.5f,
                    Is.EqualTo(0.5f).Within(0.0001f));

                stageChanged.Invoke(SceneLoadStage.WaitingForConfirmation);
                Assert.That(loadingRoot.activeSelf, Is.False);
                Assert.That(readyRoot.activeSelf, Is.True);
                Assert.That(continueObject.activeSelf, Is.True);
                Assert.That(continueObject.GetComponent<Button>().interactable, Is.True);
            }
            finally
            {
                UnityEngine.Object.Destroy(managerObject);
                UnityEngine.Object.Destroy(screenObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator LoadingScreen_PersistsThroughReadiness_AndReleasesOnCompleted()
        {
            GameObject managerObject = new GameObject("LoadingScreenPersistenceManager");
            GameObject loadingRoot = new GameObject("LoadingScreenPersistenceRoot");
            GameObject screenObject = new GameObject("LoadingScreenPersistenceView");
            screenObject.transform.SetParent(loadingRoot.transform, false);

            try
            {
                GameSceneManager sceneManager = managerObject.AddComponent<GameSceneManager>();
                LoadingScreen loadingScreen = screenObject.AddComponent<LoadingScreen>();
                loadingScreen.Construct(sceneManager);

                Assert.That(loadingRoot.scene.name, Is.EqualTo("DontDestroyOnLoad"));

                FieldInfo stageEvent = typeof(GameSceneManager).GetField(
                    "LoadStageChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(stageEvent, Is.Not.Null);
                Action<SceneLoadStage> stageChanged =
                    stageEvent.GetValue(sceneManager) as Action<SceneLoadStage>;
                Assert.That(stageChanged, Is.Not.Null);

                stageChanged.Invoke(SceneLoadStage.WaitingForReadiness);
                yield return null;
                Assert.That(loadingRoot, Is.Not.Null);

                stageChanged.Invoke(SceneLoadStage.Completed);
                yield return null;
                Assert.That(loadingRoot == null, Is.True);
            }
            finally
            {
                UnityEngine.Object.Destroy(managerObject);
                UnityEngine.Object.Destroy(loadingRoot);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator LoadProgressReporter_CapsBeforeSceneActivation()
        {
            GameObject managerObject = new GameObject("LoadProgressContractTest");
            try
            {
                GameSceneManager sceneManager = managerObject.AddComponent<GameSceneManager>();
                float maximumProgress = 0f;
                float lastProgress = 0f;
                int progressEventCount = 0;
                sceneManager.LoadProgressChanged += progress =>
                {
                    maximumProgress = Mathf.Max(maximumProgress, progress);
                    lastProgress = progress;
                    progressEventCount++;
                };

                MethodInfo reporter = typeof(GameSceneManager).GetMethod(
                    "ReportLoadProgressAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(reporter, Is.Not.Null);

                AsyncOperation operation = Resources.UnloadUnusedAssets();
                UniTask task = (UniTask)reporter.Invoke(
                    sceneManager,
                    new object[] { operation, 0f, CancellationToken.None });
                yield return task.ToCoroutine();

                Assert.That(progressEventCount, Is.GreaterThan(0));
                Assert.That(
                    maximumProgress,
                    Is.LessThanOrEqualTo(GameSceneManager.SceneStreamingProgressCeiling + 0.0001f));
                Assert.That(
                    lastProgress,
                    Is.EqualTo(GameSceneManager.SceneStreamingProgressCeiling).Within(0.0001f));
                Assert.That(lastProgress, Is.LessThan(1f));
            }
            finally
            {
                UnityEngine.Object.Destroy(managerObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneReadiness_WaitsForReady_AndNeverPublishesOneEarly()
        {
            GameObject managerObject = new GameObject("SceneReadinessContractTest");
            try
            {
                GameSceneManager sceneManager = managerObject.AddComponent<GameSceneManager>();
                SetField(sceneManager, "isLoading", true);
                SetField(sceneManager, "sceneReadinessRequired", true);
                SetField(sceneManager, "sceneReadinessReady", false);
                SetField(sceneManager, "currentLoadStage", SceneLoadStage.WaitingForReadiness);

                float maximumProgress = 0f;
                sceneManager.LoadProgressChanged += progress =>
                    maximumProgress = Mathf.Max(maximumProgress, progress);

                MethodInfo waiter = typeof(GameSceneManager).GetMethod(
                    "AwaitSceneReadinessAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(waiter, Is.Not.Null);

                UniTask readinessTask = (UniTask)waiter.Invoke(
                    sceneManager,
                    new object[] { "Game" });
                Assert.That(readinessTask.GetAwaiter().IsCompleted, Is.False);

                sceneManager.ReportSceneReadinessProgress(1f);
                Assert.That(
                    maximumProgress,
                    Is.EqualTo(GameSceneManager.SceneReadinessProgressCeiling).Within(0.0001f));
                Assert.That(maximumProgress, Is.LessThan(1f));

                yield return null;
                Assert.That(readinessTask.GetAwaiter().IsCompleted, Is.False);

                sceneManager.ReportSceneReady();
                yield return readinessTask.ToCoroutine();
                Assert.That(maximumProgress, Is.LessThan(1f));
            }
            finally
            {
                UnityEngine.Object.Destroy(managerObject);
            }

            yield return null;
        }

        [Test]
        public void SceneLoadStageTopology_AllowsOnlyExpectedTransitions()
        {
            MethodInfo validator = typeof(GameSceneManager).GetMethod(
                "IsValidStageTransition",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(validator, Is.Not.Null);

            bool Valid(SceneLoadStage current, SceneLoadStage next)
                => (bool)validator.Invoke(null, new object[] { current, next });

            Assert.That(Valid(SceneLoadStage.Idle, SceneLoadStage.LoadingPresentation), Is.True);
            Assert.That(Valid(SceneLoadStage.LoadingPresentation, SceneLoadStage.StreamingTarget), Is.True);
            Assert.That(Valid(SceneLoadStage.StreamingTarget, SceneLoadStage.AwaitingActivation), Is.True);
            Assert.That(Valid(SceneLoadStage.AwaitingActivation, SceneLoadStage.ActivatingTarget), Is.True);
            Assert.That(Valid(SceneLoadStage.ActivatingTarget, SceneLoadStage.FinalizingTarget), Is.True);
            Assert.That(Valid(SceneLoadStage.FinalizingTarget, SceneLoadStage.WaitingForReadiness), Is.True);
            Assert.That(Valid(SceneLoadStage.FinalizingTarget, SceneLoadStage.WaitingForConfirmation), Is.True);
            Assert.That(Valid(SceneLoadStage.WaitingForReadiness, SceneLoadStage.WaitingForConfirmation), Is.True);
            Assert.That(Valid(SceneLoadStage.WaitingForReadiness, SceneLoadStage.UnloadingPresentation), Is.True);
            Assert.That(Valid(SceneLoadStage.WaitingForConfirmation, SceneLoadStage.UnloadingPresentation), Is.True);
            Assert.That(Valid(SceneLoadStage.FinalizingTarget, SceneLoadStage.UnloadingPresentation), Is.True);
            Assert.That(Valid(SceneLoadStage.UnloadingPresentation, SceneLoadStage.Completed), Is.True);
            Assert.That(Valid(SceneLoadStage.Completed, SceneLoadStage.Idle), Is.True);
            Assert.That(Valid(SceneLoadStage.StreamingTarget, SceneLoadStage.Failed), Is.True);
            Assert.That(Valid(SceneLoadStage.Failed, SceneLoadStage.Idle), Is.True);

            Assert.That(Valid(SceneLoadStage.StreamingTarget, SceneLoadStage.Completed), Is.False);
            Assert.That(Valid(SceneLoadStage.Failed, SceneLoadStage.ActivatingTarget), Is.False);
            Assert.That(Valid(SceneLoadStage.Idle, SceneLoadStage.FinalizingTarget), Is.False);
            Assert.That(Valid(SceneLoadStage.WaitingForConfirmation, SceneLoadStage.Completed), Is.False);
        }

        [Test]
        public void ConfirmationGate_OnlyAcceptsOneConfirmationWhileWaiting()
        {
            GameObject managerObject = new GameObject("SceneConfirmationGateTest");
            try
            {
                GameSceneManager sceneManager = managerObject.AddComponent<GameSceneManager>();
                SetField(sceneManager, "isLoading", true);
                SetField(
                    sceneManager,
                    "currentCompletionMode",
                    SceneLoadCompletionMode.RequireConfirmation);
                SetField(
                    sceneManager,
                    "currentLoadStage",
                    SceneLoadStage.WaitingForConfirmation);
                SetField(
                    sceneManager,
                    "transitionConfirmationSource",
                    new UniTaskCompletionSource());

                Assert.That(sceneManager.IsWaitingForConfirmation, Is.True);
                Assert.That(sceneManager.ConfirmCurrentTransition(), Is.True);
                Assert.That(sceneManager.ConfirmCurrentTransition(), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [UnityTest]
        public IEnumerator StartupRegistrations_ResolveAllTasks_AndRunOnlyWhenExplicitlyStarted()
        {
            GameObject root = new GameObject("StartupRegistrationProbe");
            root.SetActive(false);
            StartupProbeRootLifetimeScope scope = root.AddComponent<StartupProbeRootLifetimeScope>();
            scope.autoRun = false;
            root.SetActive(true);
            scope.Build();

            try
            {
                IStartupCoordinator coordinator = scope.Container.Resolve<IStartupCoordinator>();
                StartupProbeTaskA first = scope.Container.Resolve<StartupProbeTaskA>();
                StartupProbeTaskB second = scope.Container.Resolve<StartupProbeTaskB>();

                Assert.That(coordinator.IsReady, Is.False);
                Assert.That(first.Attempts, Is.Zero);
                Assert.That(second.Attempts, Is.Zero);

                yield return coordinator.RunAsync().ToCoroutine();

                Assert.That(coordinator.IsReady, Is.True);
                Assert.That(coordinator.Progress, Is.EqualTo(1f));
                Assert.That(first.Attempts, Is.EqualTo(1));
                Assert.That(second.Attempts, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator UIManager_ClearOwnedViews_DisablesThenDestroysRegisteredInstances()
        {
            GameObject root = new GameObject("UIManagerOwnershipTest");
            GameObject popupObject = new GameObject(
                "OwnedPopup",
                typeof(RectTransform),
                typeof(CanvasGroup));
            GameObject screenObject = new GameObject("OwnedScreen", typeof(RectTransform));
            try
            {
                UIManager uiManager = root.AddComponent<UIManager>();
                OwnedPopupProbe popup = popupObject.AddComponent<OwnedPopupProbe>();
                OwnedScreenProbe screen = screenObject.AddComponent<OwnedScreenProbe>();
                uiManager.RegisterPopup(popup);
                uiManager.RegisterScreen(screen);

                Assert.That(popupObject.activeSelf, Is.True);
                Assert.That(screenObject.activeSelf, Is.True);

                uiManager.ClearPopups();
                uiManager.ClearScreens();

                Assert.That(popupObject.activeSelf, Is.False);
                Assert.That(screenObject.activeSelf, Is.False);
                Assert.That(uiManager.GetPopup<OwnedPopupProbe>(), Is.Null);
                Assert.That(uiManager.GetScreen<OwnedScreenProbe>(), Is.Null);

                yield return null;
                Assert.That(popup == null, Is.True);
                Assert.That(screen == null, Is.True);
            }
            finally
            {
                if (popupObject != null)
                {
                    UnityEngine.Object.Destroy(popupObject);
                }
                if (screenObject != null)
                {
                    UnityEngine.Object.Destroy(screenObject);
                }
                UnityEngine.Object.Destroy(root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator RippleSurfaceClock_AdvancesOnGpuClockAndPreservesPauseResume()
        {
            GameObject surfaceObject = new GameObject("RippleClockTest");
            surfaceObject.SetActive(false);
            surfaceObject.AddComponent<MeshRenderer>();
            HP.Framework.Graphics.RippleSurfaceController controller =
                surfaceObject.AddComponent<HP.Framework.Graphics.RippleSurfaceController>();
            surfaceObject.SetActive(true);

            try
            {
                float start = controller.EffectTime;
                yield return new WaitForSeconds(0.05f);
                float advanced = controller.EffectTime;
                Assert.That(advanced, Is.GreaterThan(start + 0.01f));

                controller.Pause();
                float paused = controller.EffectTime;
                yield return new WaitForSeconds(0.05f);
                Assert.That(controller.EffectTime, Is.EqualTo(paused).Within(0.005f));

                controller.SetTimeScale(2f);
                controller.Resume();
                yield return new WaitForSeconds(0.05f);
                Assert.That(controller.EffectTime, Is.GreaterThan(paused + 0.05f));
            }
            finally
            {
                UnityEngine.Object.Destroy(surfaceObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator BubblingSurfaceClock_AdvancesOnGpuClockAndPreservesPauseResume()
        {
            GameObject surfaceObject = new GameObject("BubblingClockTest");
            surfaceObject.SetActive(false);
            MeshRenderer renderer = surfaceObject.AddComponent<MeshRenderer>();
            HP.Framework.Graphics.BubblingSurfaceController controller =
                surfaceObject.AddComponent<HP.Framework.Graphics.BubblingSurfaceController>();
            surfaceObject.SetActive(true);

            var propertyBlock = new MaterialPropertyBlock();
            int effectTimeId = Shader.PropertyToID("_EffectTime");
            int effectTimeScaleId = Shader.PropertyToID("_EffectTimeScale");

            try
            {
                renderer.GetPropertyBlock(propertyBlock);
                float gpuBaseTime = propertyBlock.GetFloat(effectTimeId);
                Assert.That(propertyBlock.GetFloat(effectTimeScaleId), Is.GreaterThan(0f));

                float start = controller.EffectTime;
                yield return new WaitForSeconds(0.05f);
                float advanced = controller.EffectTime;
                Assert.That(advanced, Is.GreaterThan(start + 0.01f));

                renderer.GetPropertyBlock(propertyBlock);
                Assert.That(
                    propertyBlock.GetFloat(effectTimeId),
                    Is.EqualTo(gpuBaseTime).Within(0.001f),
                    "Scaled runtime playback should advance in the shader without rewriting _EffectTime every frame.");

                controller.PauseAnimation();
                float paused = controller.EffectTime;
                renderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetFloat(effectTimeScaleId), Is.EqualTo(0f).Within(0.0001f));
                yield return new WaitForSeconds(0.05f);
                Assert.That(controller.EffectTime, Is.EqualTo(paused).Within(0.005f));

                controller.SetAnimationTimeScale(2f);
                controller.ResumeAnimation();
                yield return new WaitForSeconds(0.05f);
                Assert.That(controller.EffectTime, Is.GreaterThan(paused + 0.05f));

                controller.enabled = false;
                float disabledTime = controller.EffectTime;
                renderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetFloat(effectTimeScaleId), Is.EqualTo(0f).Within(0.0001f));
                yield return new WaitForSeconds(0.05f);
                Assert.That(controller.EffectTime, Is.EqualTo(disabledTime).Within(0.005f));
            }
            finally
            {
                UnityEngine.Object.Destroy(surfaceObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator BubblingSurfaceClock_UnscaledFallbackAdvancesWhenGameTimeIsPaused()
        {
            float originalTimeScale = Time.timeScale;
            GameObject surfaceObject = new GameObject("BubblingUnscaledClockTest");
            surfaceObject.SetActive(false);
            MeshRenderer renderer = surfaceObject.AddComponent<MeshRenderer>();
            HP.Framework.Graphics.BubblingSurfaceController controller =
                surfaceObject.AddComponent<HP.Framework.Graphics.BubblingSurfaceController>();
            SetField(controller, "useUnscaledTime", true);
            surfaceObject.SetActive(true);

            var propertyBlock = new MaterialPropertyBlock();
            int effectTimeScaleId = Shader.PropertyToID("_EffectTimeScale");

            try
            {
                renderer.GetPropertyBlock(propertyBlock);
                Assert.That(
                    propertyBlock.GetFloat(effectTimeScaleId),
                    Is.EqualTo(0f).Within(0.0001f),
                    "Unscaled-time playback should use the CPU fallback instead of shader _Time.");

                Time.timeScale = 0f;
                float start = controller.EffectTime;
                yield return new WaitForSecondsRealtime(0.05f);
                Assert.That(controller.EffectTime, Is.GreaterThan(start + 0.01f));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                UnityEngine.Object.Destroy(surfaceObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ChildScope_CanShadowUIAndPool_WithItsOwnResolver()
        {
            GameObject root = CreateConfiguredBootstrap();
            GameObject pooledPrefab = null;
            GameObject popupPrefabObject = null;
            try
            {
                root.SetActive(true);
                yield return null;

                RootLifetimeScope rootScope = root.GetComponent<RootLifetimeScope>();
                IUIService rootUi = rootScope.Container.Resolve<IUIService>();
                IPoolService rootPool = rootScope.Container.Resolve<IPoolService>();
                IEventBus rootEventBus = rootScope.Container.Resolve<IEventBus>();

                BaseSceneLifetimeScope sceneScope =
                    rootScope.CreateChild<BaseSceneLifetimeScope>(builder =>
                    {
                        builder.Register<ScopeTestDependency>(Lifetime.Scoped).AsSelf();
                        builder.RegisterScopeEventBus();
                        builder.RegisterScopedPool();
                        builder.RegisterScopedUI();
                    }, "ScopedInfrastructureTest");

                IUIService sceneUi = sceneScope.Container.Resolve<IUIService>();
                IPoolService scenePool = sceneScope.Container.Resolve<IPoolService>();
                IScopedUIService scopedUi = sceneScope.Container.Resolve<IScopedUIService>();
                IEventBus sceneEventBus = sceneScope.Container.Resolve<IEventBus>();
                ScopeTestDependency dependency =
                    sceneScope.Container.Resolve<ScopeTestDependency>();

                Assert.That(sceneUi, Is.TypeOf<ScopedUIService>());
                Assert.That(scenePool, Is.TypeOf<ScopedPoolService>());
                Assert.That(sceneUi, Is.Not.SameAs(rootUi));
                Assert.That(scenePool, Is.Not.SameAs(rootPool));
                Assert.That(sceneEventBus, Is.Not.SameAs(rootEventBus));

                pooledPrefab = new GameObject("InjectedPoolProbePrefab");
                pooledPrefab.SetActive(false);
                pooledPrefab.AddComponent<ScopedPoolInjectionProbe>();
                GameObject pooledInstance = scenePool.Get(pooledPrefab);
                ScopedPoolInjectionProbe pooledProbe =
                    pooledInstance.GetComponent<ScopedPoolInjectionProbe>();
                Assert.That(pooledProbe.Dependency, Is.SameAs(dependency));
                scenePool.Release(pooledInstance);

                popupPrefabObject = new GameObject(
                    "ScopedPopupProbePrefab",
                    typeof(RectTransform),
                    typeof(CanvasGroup));
                popupPrefabObject.SetActive(false);
                ScopedPopupInjectionProbe popupPrefab =
                    popupPrefabObject.AddComponent<ScopedPopupInjectionProbe>();

                ScopedPopupInjectionProbe popup = scopedUi.OpenPopup(popupPrefab);
                Assert.That(popup, Is.Not.Null);
                Assert.That(popup.Dependency, Is.SameAs(dependency));
                Assert.That(popup.InjectedUIService, Is.SameAs(sceneUi));

                UnityEngine.Object.Destroy(sceneScope.gameObject);
                yield return null;

                Assert.That(dependency.IsDisposed, Is.True);
                Assert.That(popup == null, Is.True,
                    "Scope-owned popup should be destroyed with the child scope.");
            }
            finally
            {
                if (pooledPrefab != null)
                {
                    UnityEngine.Object.Destroy(pooledPrefab);
                }

                if (popupPrefabObject != null)
                {
                    UnityEngine.Object.Destroy(popupPrefabObject);
                }

                UnityEngine.Object.Destroy(root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Bootstrap_BuildsContainer_AndResolvesCoreServices()
        {
            GameObject root = CreateConfiguredBootstrap();
            try
            {
                root.SetActive(true);
                yield return null;

                RootLifetimeScope scope = root.GetComponent<RootLifetimeScope>();
                Assert.That(scope.Container, Is.Not.Null);

                Assert.That(scope.Container.Resolve<ISettingsService>(), Is.Not.Null);
                IApplicationLifecycle lifecycle = scope.Container.Resolve<IApplicationLifecycle>();
                Assert.That(lifecycle, Is.Not.Null);
                Assert.That(scope.Container.Resolve<ApplicationLifecycleService>(), Is.SameAs(lifecycle));
                Assert.That(scope.Container.Resolve<IEventBus>(), Is.Not.Null);
                Assert.That(scope.Container.Resolve<IProcedureService>(), Is.Not.Null);
                IAssetProvider assetProvider = scope.Container.Resolve<IAssetProvider>();
                IAssetLeaseProvider assetLeaseProvider = scope.Container.Resolve<IAssetLeaseProvider>();
                IAssetMemoryService assetMemory = scope.Container.Resolve<IAssetMemoryService>();
                IAssetDiagnostics assetDiagnostics = scope.Container.Resolve<IAssetDiagnostics>();
                ResourcesAssetProvider resourcesProvider = scope.Container.Resolve<ResourcesAssetProvider>();
                Assert.That(assetProvider, Is.SameAs(resourcesProvider));
                Assert.That(assetLeaseProvider, Is.SameAs(resourcesProvider));
                Assert.That(assetMemory, Is.SameAs(resourcesProvider));
                Assert.That(assetDiagnostics, Is.SameAs(resourcesProvider));
                Assert.That(scope.Container.Resolve<IAudioService>(), Is.Not.Null);
                Assert.That(scope.Container.Resolve<IUIService>(), Is.Not.Null);
                Assert.That(scope.Container.Resolve<IPoolService>(), Is.Not.Null);
                Assert.That(scope.Container.Resolve<IHapticService>(), Is.Not.Null);
                Assert.That(scope.Container.Resolve<GameSceneManager>(), Is.Not.Null);
                Assert.That(scope.Container.Resolve<InputManager>(), Is.Not.Null);
                IStartupCoordinator startup = scope.Container.Resolve<IStartupCoordinator>();
                Assert.That(startup, Is.Not.Null);
                Assert.That(startup.IsReady, Is.False,
                    "Container construction must not implicitly mean async application readiness.");
                yield return startup.RunAsync().ToCoroutine();
                Assert.That(startup.IsReady, Is.True);
                Assert.That(startup.Progress, Is.EqualTo(1f));

                SettingsManager settings = scope.Container.Resolve<SettingsManager>();
                Assert.That(settings.IsInitialized, Is.True);

                GameSceneManager sceneManager = scope.Container.Resolve<GameSceneManager>();
                Assert.That(sceneManager.CurrentSceneName, Is.Not.Null.And.Not.Empty);
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Bootstrap_DisposeUnsubscribesLifecycle_AndRebuildDoesNotLeak()
        {
            GameObject firstRoot = CreateConfiguredBootstrap();
            firstRoot.SetActive(true);
            yield return null;

            RootLifetimeScope firstScope = firstRoot.GetComponent<RootLifetimeScope>();
            ApplicationLifecycleService firstLifecycle =
                firstScope.Container.Resolve<ApplicationLifecycleService>();
            Assert.That(firstLifecycle.IsLowMemorySubscribed, Is.True);

            firstScope.Dispose();
            Assert.That(firstLifecycle.IsLowMemorySubscribed, Is.False);
            yield return null;

            GameObject secondRoot = CreateConfiguredBootstrap();
            secondRoot.SetActive(true);
            yield return null;

            RootLifetimeScope secondScope = secondRoot.GetComponent<RootLifetimeScope>();
            ApplicationLifecycleService secondLifecycle =
                secondScope.Container.Resolve<ApplicationLifecycleService>();
            Assert.That(secondLifecycle.IsLowMemorySubscribed, Is.True);

            secondScope.Dispose();
            Assert.That(secondLifecycle.IsLowMemorySubscribed, Is.False);
            yield return null;
        }

        private static GameObject CreateConfiguredBootstrap()
        {
            GameObject root = new GameObject("BootstrapPlayModeTest");
            root.SetActive(false);

            RootLifetimeScope scope = root.AddComponent<RootLifetimeScope>();
            Transform lifecycleRoot = CreateChild(root.transform, "Lifecycle");
            ApplicationLifecycleService lifecycle = lifecycleRoot.gameObject.AddComponent<ApplicationLifecycleService>();
            Transform audioRoot = CreateChild(root.transform, "Audio");
            AudioManager audioManager = audioRoot.gameObject.AddComponent<AudioManager>();
            Transform uiRoot = CreateChild(root.transform, "UI");
            UIManager uiManager = uiRoot.gameObject.AddComponent<UIManager>();
            Transform inputRoot = CreateChild(root.transform, "Input");
            InputManager inputManager = inputRoot.gameObject.AddComponent<InputManager>();
            Transform sceneRoot = CreateChild(root.transform, "Scene");
            GameSceneManager sceneManager = sceneRoot.gameObject.AddComponent<GameSceneManager>();
            Transform poolsRoot = CreateChild(root.transform, "Pools");
            PoolManager poolManager = poolsRoot.gameObject.AddComponent<PoolManager>();
            Transform hapticsRoot = CreateChild(root.transform, "Haptics");
            HapticManager hapticManager = hapticsRoot.gameObject.AddComponent<HapticManager>();

            Camera uiCamera = CreateChild(uiRoot, "UICamera")
                .gameObject.AddComponent<Camera>();
            RectTransform canvasRoot = CreateRectChild(uiRoot, "UICanvas");
            RectTransform screenRoot = CreateRectChild(canvasRoot, "ScreenRoot");
            RectTransform popupRoot = CreateRectChild(canvasRoot, "PopupRoot");
            RectTransform notificationRoot = CreateRectChild(canvasRoot, "NotificationRoot");
            RectTransform inputBlocker = CreateRectChild(canvasRoot, "InputBlocker");

            SetField(scope, "applicationLifecycle", lifecycle);
            SetField(scope, "audioManager", audioManager);
            SetField(scope, "uiManager", uiManager);
            SetField(scope, "inputManager", inputManager);
            SetField(scope, "gameSceneManager", sceneManager);
            SetField(scope, "poolManager", poolManager);
            SetField(scope, "hapticManager", hapticManager);

            SetField(uiManager, "screenRoot", screenRoot);
            SetField(uiManager, "popupRoot", popupRoot);
            SetField(uiManager, "notificationRoot", notificationRoot);
            SetField(uiManager, "inputBlocker", inputBlocker);
            SetField(uiManager, "uiCamera", uiCamera);
            SetField(poolManager, "rootPoolParent", poolsRoot);

            return root;
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static RectTransform CreateRectChild(Transform parent, string name)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rectTransform = child.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Serialized field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }
    }
}
