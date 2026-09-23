namespace HP.Framework.Bootstrap
{
    using System;
    using System.Collections;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using HP.Framework;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using VContainer;
    using VContainer.Unity;

    public enum SceneLoadStage
    {
        Idle = 0,
        LoadingPresentation = 1,
        StreamingTarget = 2,
        AwaitingActivation = 3,
        ActivatingTarget = 4,
        FinalizingTarget = 5,
        UnloadingPresentation = 6,
        Completed = 7,
        Failed = 8,
        WaitingForReadiness = 9
    }

    /// <summary>
    /// Cancellation-aware scene transition service. Every request completes, throws, or is cancelled.
    /// Unity cannot stop an AsyncOperation after it starts, so cancellation releases scene activation
    /// before reporting OperationCanceledException to prevent a permanently stalled load.
    /// </summary>
    public sealed class GameSceneManager : MonoBehaviour, IProcedureSceneLoader, IInitializable
    {
        public event Action<string> SceneLoadStarted;
        public event Action<string> SceneLoadCompleted;
        public event Action<string> SceneUnloadCompleted;
        public const float SceneStreamingProgressCeiling = 0.8f;
        public const float SceneActivationProgressCeiling = 0.9f;
        public const float SceneReadinessProgressCeiling = 0.99f;
        public const float ActivationProgressCeiling = SceneReadinessProgressCeiling;

        public event Action<float> LoadProgressChanged;
        public event Action<SceneLoadStage> LoadStageChanged;
        public event Action<string, Exception> SceneLoadFailed;

        [Header("Loading Settings")]
        [Tooltip("Compatibility name: this is a minimum loading-presentation duration, not fake progress.")]
        [SerializeField, Min(0f)] private float defaultFakeLoadingDuration;
        [SerializeField, Min(0f)] private float activationWarningThresholdSeconds = 5f;
        [SerializeField, Min(1f)] private float readinessTimeoutSeconds = 30f;
        [SerializeField] private string loadingSceneName = BaseConstants.DefaultLoadingSceneName;

        private ProcedureManager procedureManager;
        private RootLifetimeScope rootLifetimeScope;
        private bool isLoading;
        private string activeSceneName;
        private SceneLoadStage currentLoadStage = SceneLoadStage.Idle;
        private double currentLoadStageStartedAt;
        private bool sceneReadinessRequired;
        private bool sceneReadinessReady;
        private Exception sceneReadinessFailure;
        private float sceneReadinessProgress;

        public bool IsLoading => isLoading;
        public string CurrentSceneName => activeSceneName;
        public SceneLoadStage CurrentLoadStage => currentLoadStage;
        public bool IsSceneReadinessExpected => isLoading && sceneReadinessRequired;

        [Inject]
        public void Construct(
            ProcedureManager procedureManager,
            RootLifetimeScope rootLifetimeScope)
        {
            this.procedureManager = procedureManager;
            this.rootLifetimeScope = rootLifetimeScope;
        }

        public void Initialize()
        {
            activeSceneName = SceneManager.GetActiveScene().name;
            isLoading = false;
            currentLoadStage = SceneLoadStage.Idle;
            currentLoadStageStartedAt = Time.realtimeSinceStartupAsDouble;
            procedureManager?.RegisterSceneLoader(this);
        }

        public UniTask<Scene> LoadSceneAsync(
            string sceneName,
            float fakeLoadingDuration = 0f,
            CancellationToken cancellationToken = default)
        {
            return LoadSceneInternalAsync(
                sceneName,
                LoadSceneMode.Single,
                setActiveScene: true,
                minimumLoadingDisplayDuration: fakeLoadingDuration,
                waitForSceneReadiness: false,
                cancellationToken: cancellationToken);
        }

        public UniTask<Scene> LoadSceneWithReadinessAsync(
            string sceneName,
            float minimumLoadingDisplayDuration = 0f,
            CancellationToken cancellationToken = default)
        {
            return LoadSceneInternalAsync(
                sceneName,
                LoadSceneMode.Single,
                setActiveScene: true,
                minimumLoadingDisplayDuration: minimumLoadingDisplayDuration,
                waitForSceneReadiness: true,
                cancellationToken: cancellationToken);
        }

        public UniTask<Scene> LoadSceneAsync(
            string sceneName,
            LoadSceneMode loadMode,
            bool setActiveScene,
            float fakeLoadingDuration = 0f,
            CancellationToken cancellationToken = default)
        {
            return LoadSceneInternalAsync(
                sceneName,
                loadMode,
                setActiveScene,
                minimumLoadingDisplayDuration: fakeLoadingDuration,
                waitForSceneReadiness: false,
                cancellationToken: cancellationToken);
        }

        private async UniTask<Scene> LoadSceneInternalAsync(
            string sceneName,
            LoadSceneMode loadMode,
            bool setActiveScene,
            float minimumLoadingDisplayDuration,
            bool waitForSceneReadiness,
            CancellationToken cancellationToken)
        {
            if (currentLoadStage == SceneLoadStage.Failed)
            {
                SetLoadStage(SceneLoadStage.Idle);
            }

            ValidateLoadRequest(sceneName);

            isLoading = true;
            BeginSceneReadiness(waitForSceneReadiness);
            AsyncOperation targetOperation = null;
            IDisposable parentOverride = null;
            bool loadSucceeded = false;
            bool failureReported = false;
            bool targetActivated = false;
            bool preserveFailurePresentation = false;

            try
            {
                SetLoadStage(SceneLoadStage.LoadingPresentation);
                cancellationToken.ThrowIfCancellationRequested();
                SceneLoadStarted?.Invoke(sceneName);
                parentOverride = LifetimeScope.EnqueueParent(rootLifetimeScope);
                await TryLoadLoadingSceneAsync(sceneName, cancellationToken);

                SetLoadStage(SceneLoadStage.StreamingTarget);
                targetOperation = SceneManager.LoadSceneAsync(sceneName, loadMode);
                if (targetOperation == null)
                {
                    throw new InvalidOperationException(
                        $"Unity failed to create a load operation for scene '{sceneName}'.");
                }

                targetOperation.allowSceneActivation = false;
                await ReportLoadProgressAsync(
                    targetOperation,
                    minimumLoadingDisplayDuration,
                    cancellationToken);

                SetLoadStage(SceneLoadStage.AwaitingActivation);
                targetOperation.allowSceneActivation = true;
                SetLoadStage(SceneLoadStage.ActivatingTarget);
                await AwaitTargetActivationAsync(targetOperation, sceneName);
                targetActivated = true;

                SetLoadStage(SceneLoadStage.FinalizingTarget);
                Scene loadedScene = SceneManager.GetSceneByName(sceneName);
                if (!loadedScene.IsValid() || !loadedScene.isLoaded)
                {
                    throw new InvalidOperationException(
                        $"Scene '{sceneName}' completed loading but Unity did not return a valid loaded scene.");
                }

                if (setActiveScene
                    && SceneManager.GetActiveScene() != loadedScene
                    && !SceneManager.SetActiveScene(loadedScene))
                {
                    throw new InvalidOperationException(
                        $"Failed to set scene '{sceneName}' as the active scene.");
                }

                if (loadMode == LoadSceneMode.Single || setActiveScene)
                {
                    activeSceneName = loadedScene.name;
                }

                LoadProgressChanged?.Invoke(SceneActivationProgressCeiling);
                if (waitForSceneReadiness)
                {
                    SetLoadStage(SceneLoadStage.WaitingForReadiness);
                    await AwaitSceneReadinessAsync(sceneName);
                }

                LoadProgressChanged?.Invoke(1f);
                SceneLoadCompleted?.Invoke(sceneName);
                loadSucceeded = true;
                return loadedScene;
            }
            catch (OperationCanceledException exception)
            {
                SetLoadStage(SceneLoadStage.Failed);
                if (targetOperation != null && !targetOperation.isDone)
                {
                    targetOperation.allowSceneActivation = true;
                    await AwaitOperationAsync(targetOperation, CancellationToken.None);
                }

                targetActivated = targetActivated || (targetOperation != null && targetOperation.isDone);
                preserveFailurePresentation = targetActivated;
                SceneLoadFailed?.Invoke(sceneName, exception);
                failureReported = true;
                throw;
            }
            catch (Exception exception)
            {
                SetLoadStage(SceneLoadStage.Failed);
                targetActivated = targetActivated || (targetOperation != null && targetOperation.isDone);
                preserveFailurePresentation = targetActivated;
                SceneLoadFailed?.Invoke(sceneName, exception);
                failureReported = true;
                throw;
            }
            finally
            {
                try
                {
                    if (loadSucceeded)
                    {
                        SetLoadStage(SceneLoadStage.UnloadingPresentation);
                    }

                    if (!preserveFailurePresentation)
                    {
                        await UnloadLoadingSceneIfPresentAsync();
                    }
                }
                catch (Exception cleanupException)
                {
                    if (loadSucceeded)
                    {
                        SetLoadStage(SceneLoadStage.Failed);
                        if (!failureReported)
                        {
                            SceneLoadFailed?.Invoke(sceneName, cleanupException);
                        }

                        throw;
                    }

                    BaseLog.LogError(
                        $"[GameSceneManager] Failed to unload loading presentation after '{sceneName}' failed: {cleanupException}");
                }
                finally
                {
                    try
                    {
                        parentOverride?.Dispose();
                    }
                    finally
                    {
                        isLoading = false;
                        if (loadSucceeded && currentLoadStage != SceneLoadStage.Failed)
                        {
                            SetLoadStage(SceneLoadStage.Completed);
                        }

                        if (!preserveFailurePresentation && currentLoadStage != SceneLoadStage.Idle)
                        {
                            SetLoadStage(SceneLoadStage.Idle);
                        }

                        ResetSceneReadiness();
                    }
                }
            }
        }

        public UniTask<Scene> ReloadActiveSceneAsync(CancellationToken cancellationToken = default)
        {
            string sceneName = string.IsNullOrWhiteSpace(activeSceneName)
                ? SceneManager.GetActiveScene().name
                : activeSceneName;

            return LoadSceneAsync(
                sceneName,
                LoadSceneMode.Single,
                setActiveScene: true,
                fakeLoadingDuration: 0f,
                cancellationToken: cancellationToken);
        }

        public UniTask<Scene> ReloadActiveSceneWithReadinessAsync(
            CancellationToken cancellationToken = default)
        {
            string sceneName = string.IsNullOrWhiteSpace(activeSceneName)
                ? SceneManager.GetActiveScene().name
                : activeSceneName;

            return LoadSceneInternalAsync(
                sceneName,
                LoadSceneMode.Single,
                setActiveScene: true,
                minimumLoadingDisplayDuration: 0f,
                waitForSceneReadiness: true,
                cancellationToken: cancellationToken);
        }

        public void ReportSceneReadinessProgress(float progress)
        {
            if (!IsSceneReadinessExpected || sceneReadinessReady || sceneReadinessFailure != null)
            {
                return;
            }

            sceneReadinessProgress = Mathf.Clamp01(progress);
            if (currentLoadStage == SceneLoadStage.WaitingForReadiness)
            {
                LoadProgressChanged?.Invoke(Mathf.Lerp(
                    SceneActivationProgressCeiling,
                    SceneReadinessProgressCeiling,
                    sceneReadinessProgress));
            }
        }

        public void ReportSceneReady()
        {
            if (!IsSceneReadinessExpected || sceneReadinessFailure != null)
            {
                return;
            }

            sceneReadinessProgress = 1f;
            sceneReadinessReady = true;
        }

        public void ReportSceneReadinessFailure(Exception exception)
        {
            if (!IsSceneReadinessExpected || sceneReadinessReady || sceneReadinessFailure != null)
            {
                return;
            }

            sceneReadinessFailure = exception
                ?? new InvalidOperationException("Scene readiness failed without an exception.");
        }

        public async UniTask UnloadSceneAsync(
            string sceneName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("Scene name must not be empty.", nameof(sceneName));
            }

            if (isLoading)
            {
                throw new InvalidOperationException(
                    $"Cannot unload '{sceneName}' because another scene operation is running.");
            }

            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException($"Scene '{sceneName}' is not currently loaded.");
            }

            isLoading = true;
            try
            {
                AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
                if (operation == null)
                {
                    throw new InvalidOperationException(
                        $"Unity failed to create an unload operation for scene '{sceneName}'.");
                }

                await AwaitOperationAsync(operation, cancellationToken);

                if (string.Equals(activeSceneName, sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    activeSceneName = SceneManager.GetActiveScene().name;
                }

                SceneUnloadCompleted?.Invoke(sceneName);
            }
            finally
            {
                isLoading = false;
            }
        }

        /// <summary>
        /// Compatibility wrapper for callers that still require a Coroutine handle.
        /// New code should await LoadSceneAsync directly.
        /// </summary>
        public Coroutine LoadSceneCoroutine(
            string sceneName,
            LoadSceneMode loadMode = LoadSceneMode.Single,
            bool setActiveScene = true,
            Action<Scene> onLoaded = null,
            Action<Exception> onError = null,
            float fakeLoadingDuration = 0f)
        {
            CancellationToken token = this.GetCancellationTokenOnDestroy();
            return StartCoroutine(RunSceneTask(
                LoadSceneAsync(sceneName, loadMode, setActiveScene, fakeLoadingDuration, token),
                onLoaded,
                onError));
        }

        private IEnumerator RunSceneTask(
            UniTask<Scene> task,
            Action<Scene> onLoaded,
            Action<Exception> onError)
        {
            UniTask<Scene>.Awaiter awaiter = task.GetAwaiter();
            while (!awaiter.IsCompleted)
            {
                yield return null;
            }

            try
            {
                onLoaded?.Invoke(awaiter.GetResult());
            }
            catch (Exception exception)
            {
                if (onError != null)
                {
                    onError(exception);
                }
                else
                {
                    BaseLog.LogError($"[GameSceneManager] {exception}");
                }
            }
        }

        private void ValidateLoadRequest(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("Scene name must not be empty.", nameof(sceneName));
            }

            if (isLoading)
            {
                throw new InvalidOperationException(
                    $"Cannot load '{sceneName}' because another scene operation is running.");
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                throw new InvalidOperationException(
                    $"Scene '{sceneName}' is not available in Build Settings or an AssetBundle.");
            }
        }

        private async UniTask TryLoadLoadingSceneAsync(
            string targetSceneName,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(loadingSceneName)
                || string.Equals(loadingSceneName, targetSceneName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(loadingSceneName))
            {
                throw new InvalidOperationException(
                    $"Loading scene '{loadingSceneName}' is not available. Add and enable it in Build Settings, " +
                    $"or clear {nameof(loadingSceneName)} to disable the loading scene explicitly.");
            }

            Scene existingScene = SceneManager.GetSceneByName(loadingSceneName);
            if (existingScene.IsValid() && existingScene.isLoaded)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            AsyncOperation operation = SceneManager.LoadSceneAsync(
                loadingSceneName,
                LoadSceneMode.Additive);

            if (operation == null)
            {
                throw new InvalidOperationException(
                    $"Unity failed to create a load operation for loading scene '{loadingSceneName}'.");
            }

            // Once Unity starts loading an additive scene, complete the operation before honoring
            // cancellation so the transition never leaves an orphaned AsyncOperation.
            await AwaitOperationAsync(operation, CancellationToken.None);
            cancellationToken.ThrowIfCancellationRequested();
        }

        private async UniTask ReportLoadProgressAsync(
            AsyncOperation operation,
            float requestedMinimumDisplayDuration,
            CancellationToken cancellationToken)
        {
            float minimumDisplayDuration = requestedMinimumDisplayDuration > 0f
                ? requestedMinimumDisplayDuration
                : defaultFakeLoadingDuration;
            double startedAt = Time.realtimeSinceStartupAsDouble;

            while (operation.progress < 0.89f
                   || Time.realtimeSinceStartupAsDouble - startedAt < minimumDisplayDuration)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    operation.allowSceneActivation = true;
                    cancellationToken.ThrowIfCancellationRequested();
                }

                float actualProgress = Mathf.Clamp01(operation.progress / 0.9f);
                LoadProgressChanged?.Invoke(actualProgress * SceneStreamingProgressCeiling);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            LoadProgressChanged?.Invoke(SceneStreamingProgressCeiling);
        }

        private async UniTask AwaitTargetActivationAsync(
            AsyncOperation operation,
            string sceneName)
        {
            double startedAt = Time.realtimeSinceStartupAsDouble;
            bool warningReported = false;

            while (operation != null && !operation.isDone)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!warningReported
                    && activationWarningThresholdSeconds > 0f
                    && Time.realtimeSinceStartupAsDouble - startedAt >= activationWarningThresholdSeconds)
                {
                    warningReported = true;
                    Debug.LogWarning(
                        $"[{nameof(GameSceneManager)}] Target scene '{sceneName}' activation exceeded " +
                        $"{activationWarningThresholdSeconds:0.###}s. " +
                        $"stage={currentLoadStage}, progress={operation.progress:0.###}, isDone={operation.isDone}",
                        this);
                }
#endif

                await UniTask.Yield(PlayerLoopTiming.Update, CancellationToken.None);
            }
        }

        private async UniTask AwaitSceneReadinessAsync(string sceneName)
        {
            double startedAt = Time.realtimeSinceStartupAsDouble;
            CancellationToken managerLifetime = this.GetCancellationTokenOnDestroy();

            LoadProgressChanged?.Invoke(Mathf.Lerp(
                SceneActivationProgressCeiling,
                SceneReadinessProgressCeiling,
                sceneReadinessProgress));

            while (!sceneReadinessReady)
            {
                if (sceneReadinessFailure != null)
                {
                    throw new InvalidOperationException(
                        $"Scene '{sceneName}' failed during readiness.",
                        sceneReadinessFailure);
                }

                double elapsed = Time.realtimeSinceStartupAsDouble - startedAt;
                if (readinessTimeoutSeconds > 0f && elapsed >= readinessTimeoutSeconds)
                {
                    throw new TimeoutException(
                        $"Scene '{sceneName}' did not report readiness within " +
                        $"{readinessTimeoutSeconds:0.###} seconds.");
                }

                await UniTask.Yield(PlayerLoopTiming.Update, managerLifetime);
            }
        }

        private void BeginSceneReadiness(bool required)
        {
            sceneReadinessRequired = required;
            sceneReadinessReady = !required;
            sceneReadinessFailure = null;
            sceneReadinessProgress = 0f;
        }

        private void ResetSceneReadiness()
        {
            sceneReadinessRequired = false;
            sceneReadinessReady = false;
            sceneReadinessFailure = null;
            sceneReadinessProgress = 0f;
        }


        private void SetLoadStage(SceneLoadStage nextStage)
        {
            if (currentLoadStage == nextStage)
            {
                return;
            }

            if (!IsValidStageTransition(currentLoadStage, nextStage))
            {
                throw new InvalidOperationException(
                    $"Invalid scene-load stage transition: {currentLoadStage} -> {nextStage}.");
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            double now = Time.realtimeSinceStartupAsDouble;
            if (currentLoadStage != SceneLoadStage.Idle)
            {
                double duration = Math.Max(0d, now - currentLoadStageStartedAt);
                Debug.Log(
                    $"[{nameof(GameSceneManager)}] {currentLoadStage} -> {nextStage} " +
                    $"after {duration:0.000}s.",
                    this);
            }

            currentLoadStageStartedAt = now;
#else
            currentLoadStageStartedAt = Time.realtimeSinceStartupAsDouble;
#endif
            currentLoadStage = nextStage;
            try
            {
                LoadStageChanged?.Invoke(nextStage);
            }
            catch (Exception exception)
            {
                BaseLog.LogError(
                    $"[{nameof(GameSceneManager)}] A {nameof(LoadStageChanged)} observer threw while entering {nextStage}: {exception}");
            }
        }

        private static bool IsValidStageTransition(SceneLoadStage current, SceneLoadStage next)
        {
            switch (current)
            {
                case SceneLoadStage.Idle:
                    return next == SceneLoadStage.LoadingPresentation;
                case SceneLoadStage.LoadingPresentation:
                    return next == SceneLoadStage.StreamingTarget || next == SceneLoadStage.Failed;
                case SceneLoadStage.StreamingTarget:
                    return next == SceneLoadStage.AwaitingActivation || next == SceneLoadStage.Failed;
                case SceneLoadStage.AwaitingActivation:
                    return next == SceneLoadStage.ActivatingTarget || next == SceneLoadStage.Failed;
                case SceneLoadStage.ActivatingTarget:
                    return next == SceneLoadStage.FinalizingTarget || next == SceneLoadStage.Failed;
                case SceneLoadStage.FinalizingTarget:
                    return next == SceneLoadStage.WaitingForReadiness
                           || next == SceneLoadStage.UnloadingPresentation
                           || next == SceneLoadStage.Failed;
                case SceneLoadStage.WaitingForReadiness:
                    return next == SceneLoadStage.UnloadingPresentation || next == SceneLoadStage.Failed;
                case SceneLoadStage.UnloadingPresentation:
                    return next == SceneLoadStage.Completed || next == SceneLoadStage.Failed;
                case SceneLoadStage.Completed:
                case SceneLoadStage.Failed:
                    return next == SceneLoadStage.Idle;
                default:
                    return false;
            }
        }

        private async UniTask UnloadLoadingSceneIfPresentAsync()
        {
            if (string.IsNullOrWhiteSpace(loadingSceneName))
            {
                return;
            }

            Scene scene = SceneManager.GetSceneByName(loadingSceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
            if (operation != null)
            {
                await AwaitOperationAsync(operation, CancellationToken.None);
            }
        }

        private static async UniTask AwaitOperationAsync(
            AsyncOperation operation,
            CancellationToken cancellationToken)
        {
            while (operation != null && !operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }


}
