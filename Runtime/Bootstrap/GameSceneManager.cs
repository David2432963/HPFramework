namespace HP.Framework.Bootstrap
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using HP.Framework;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using VContainer;
    using VContainer.Unity;

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
        public event Action<float> LoadProgressChanged;
        public event Action<string, Exception> SceneLoadFailed;

        [Header("Loading Settings")]
        [SerializeField, Min(0f)] private float defaultFakeLoadingDuration = 0.5f;
        [SerializeField, Min(0)] private int stutterCount;
        [SerializeField, Min(0f)] private float maxStutterDuration = 0.15f;
        [SerializeField] private string loadingSceneName = BaseConstants.DefaultLoadingSceneName;

        private readonly List<float> stutterPointsCache = new List<float>();
        private ProcedureManager procedureManager;
        private RootLifetimeScope rootLifetimeScope;
        private bool isLoading;
        private string activeSceneName;

        public bool IsLoading => isLoading;
        public string CurrentSceneName => activeSceneName;

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
            procedureManager?.RegisterSceneLoader(this);
        }

        public UniTask<Scene> LoadSceneAsync(
            string sceneName,
            float fakeLoadingDuration = 0f,
            CancellationToken cancellationToken = default)
        {
            return LoadSceneAsync(
                sceneName,
                LoadSceneMode.Single,
                setActiveScene: true,
                fakeLoadingDuration: fakeLoadingDuration,
                cancellationToken: cancellationToken);
        }

        public async UniTask<Scene> LoadSceneAsync(
            string sceneName,
            LoadSceneMode loadMode,
            bool setActiveScene,
            float fakeLoadingDuration = 0f,
            CancellationToken cancellationToken = default)
        {
            ValidateLoadRequest(sceneName);

            isLoading = true;
            AsyncOperation targetOperation = null;
            IDisposable parentOverride = null;
            SceneLoadStarted?.Invoke(sceneName);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await TryLoadLoadingSceneAsync(sceneName, cancellationToken);

                parentOverride = LifetimeScope.EnqueueParent(rootLifetimeScope);
                targetOperation = SceneManager.LoadSceneAsync(sceneName, loadMode);
                if (targetOperation == null)
                {
                    throw new InvalidOperationException(
                        $"Unity failed to create a load operation for scene '{sceneName}'.");
                }

                targetOperation.allowSceneActivation = false;
                await ReportLoadProgressAsync(targetOperation, fakeLoadingDuration, cancellationToken);

                targetOperation.allowSceneActivation = true;
                await AwaitOperationAsync(targetOperation, CancellationToken.None);
                cancellationToken.ThrowIfCancellationRequested();

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

                LoadProgressChanged?.Invoke(1f);
                SceneLoadCompleted?.Invoke(sceneName);
                return loadedScene;
            }
            catch (OperationCanceledException exception)
            {
                if (targetOperation != null && !targetOperation.isDone)
                {
                    targetOperation.allowSceneActivation = true;
                    await AwaitOperationAsync(targetOperation, CancellationToken.None);
                }

                SceneLoadFailed?.Invoke(sceneName, exception);
                throw;
            }
            catch (Exception exception)
            {
                SceneLoadFailed?.Invoke(sceneName, exception);
                throw;
            }
            finally
            {
                parentOverride?.Dispose();
                await UnloadLoadingSceneIfPresentAsync();
                isLoading = false;
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
                || string.Equals(loadingSceneName, targetSceneName, StringComparison.OrdinalIgnoreCase)
                || !Application.CanStreamedLevelBeLoaded(loadingSceneName))
            {
                return;
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
                return;
            }

            // Once Unity starts loading an additive scene, complete the operation before honoring
            // cancellation so the transition never leaves an orphaned AsyncOperation.
            await AwaitOperationAsync(operation, CancellationToken.None);
            cancellationToken.ThrowIfCancellationRequested();
        }

        private async UniTask ReportLoadProgressAsync(
            AsyncOperation operation,
            float requestedFakeDuration,
            CancellationToken cancellationToken)
        {
            float targetDuration = requestedFakeDuration > 0f
                ? requestedFakeDuration
                : defaultFakeLoadingDuration;

            BuildStutterPoints();
            float virtualProgress = 0f;
            float logicalTime = 0f;
            float currentStutterWait = 0f;
            int stutterIndex = 0;

            while (operation.progress < 0.89f || virtualProgress < 1f)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    operation.allowSceneActivation = true;
                    cancellationToken.ThrowIfCancellationRequested();
                }

                float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
                if (stutterIndex < stutterPointsCache.Count
                    && virtualProgress >= stutterPointsCache[stutterIndex])
                {
                    if (currentStutterWait <= 0f)
                    {
                        currentStutterWait = UnityEngine.Random.Range(
                            0.02f,
                            Mathf.Max(0.02f, maxStutterDuration));
                    }

                    currentStutterWait -= deltaTime;
                    if (currentStutterWait <= 0f)
                    {
                        stutterIndex++;
                    }
                }
                else
                {
                    logicalTime += deltaTime;
                }

                float fakeProgress = targetDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(logicalTime / targetDuration);
                float actualProgress = Mathf.Clamp01(operation.progress / 0.9f);
                float targetProgress = Mathf.Max(fakeProgress, actualProgress);

                if (operation.progress < 0.89f)
                {
                    targetProgress = Mathf.Min(targetProgress, 0.99f);
                }

                virtualProgress = Mathf.MoveTowards(
                    virtualProgress,
                    targetProgress,
                    deltaTime * 3f);
                LoadProgressChanged?.Invoke(virtualProgress);

                if (virtualProgress >= 1f && operation.progress >= 0.89f)
                {
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        private void BuildStutterPoints()
        {
            stutterPointsCache.Clear();
            if (stutterCount <= 0 || maxStutterDuration <= 0f)
            {
                return;
            }

            for (int i = 0; i < stutterCount; i++)
            {
                stutterPointsCache.Add(UnityEngine.Random.Range(0.15f, 0.85f));
            }

            stutterPointsCache.Sort();
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


