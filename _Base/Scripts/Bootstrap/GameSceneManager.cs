using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using Base;

/// <summary>
/// Scene loading helper for runtime transitions, managed by VContainer.
/// Loads LoadingScene additively during transitions to report smooth progress.
/// </summary>
public sealed class GameSceneManager : MonoBehaviour, IProcedureSceneLoader
{
    public event Action<string> SceneLoadStarted;
    public event Action<string> SceneLoadCompleted;
    public event Action<string> SceneUnloadCompleted;
    public event Action<float> LoadProgressChanged;

    private bool isLoading;
    private string activeSceneName;

    [Header("Fake Loading Settings")]
    [Tooltip("Default fake loading duration (seconds) if none is specified at runtime.")]
    [SerializeField, Min(0f)] private float defaultFakeLoadingDuration = 2f;
    [Tooltip("Number of random pause/stutter points while loading.")]
    [SerializeField, Min(0)] private int stutterCount = 2;
    [Tooltip("Maximum pause duration (seconds) for each stutter point.")]
    [SerializeField, Min(0f)] private float maxStutterDuration = 0.4f;
    [Tooltip("Scene name used for the loading overlay scene.")]
    [SerializeField] private string loadingSceneName = BaseConstants.DefaultLoadingSceneName;

    private readonly List<float> stutterPointsCache = new List<float>();
    private ProcedureManager procedureManager;

    public bool IsLoading => isLoading;
    public string CurrentSceneName => activeSceneName;

    [Inject]
    public void Construct(ProcedureManager procedureManager)
    {
        this.procedureManager = procedureManager;
        if (this.procedureManager != null)
        {
            this.procedureManager.RegisterSceneLoader(this);
        }
    }

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        activeSceneName = SceneManager.GetActiveScene().name;
        isLoading = false;
        if (procedureManager != null)
        {
            procedureManager.RegisterSceneLoader(this);
        }
    }

    public Coroutine LoadSceneAsync(string sceneName, LoadSceneMode loadMode = LoadSceneMode.Single, bool setActiveScene = true, Action<Scene> onLoaded = null, float fakeLoadingDuration = 0f)
    {
        return StartCoroutine(LoadSceneRoutine(sceneName, loadMode, setActiveScene, onLoaded, fakeLoadingDuration));
    }

    public Cysharp.Threading.Tasks.UniTask LoadSceneAsyncUniTask(string sceneName, LoadSceneMode loadMode = LoadSceneMode.Single, bool setActiveScene = true, Action<Scene> onLoaded = null, float fakeLoadingDuration = 0f)
    {
        var utcs = Cysharp.Threading.Tasks.AutoResetUniTaskCompletionSource.Create();
        LoadSceneAsync(sceneName, loadMode, setActiveScene, scene =>
        {
            onLoaded?.Invoke(scene);
            utcs.TrySetResult();
        }, fakeLoadingDuration);
        return utcs.Task;
    }

    public Coroutine ReloadActiveSceneAsync(Action<Scene> onLoaded = null)
    {
        if (string.IsNullOrWhiteSpace(activeSceneName))
        {
            activeSceneName = SceneManager.GetActiveScene().name;
        }

        return LoadSceneAsync(activeSceneName, LoadSceneMode.Single, true, onLoaded);
    }

    public Coroutine UnloadSceneAsync(string sceneName, Action<string> onCompleted = null)
    {
        return StartCoroutine(UnloadSceneRoutine(sceneName, onCompleted));
    }

    Coroutine IProcedureSceneLoader.LoadSceneAsync(string sceneName, float fakeLoadingDuration, Action<Scene> onLoaded)
    {
        return LoadSceneAsync(sceneName, LoadSceneMode.Single, true, onLoaded, fakeLoadingDuration);
    }

    private IEnumerator LoadSceneRoutine(string sceneName, LoadSceneMode loadMode, bool setActiveScene, Action<Scene> onLoaded, float fakeLoadingDuration = 0f)
    {
        if (isLoading || string.IsNullOrWhiteSpace(sceneName))
        {
            yield break;
        }

        isLoading = true;
        SceneLoadStarted?.Invoke(sceneName);

        // 1. Try loading LoadingScene Additively if specified and exists in Build Settings
        bool hasLoadingScene = !string.IsNullOrWhiteSpace(loadingSceneName) && Application.CanStreamedLevelBeLoaded(loadingSceneName);
        if (hasLoadingScene)
        {
            var loadingOp = SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Additive);
            while (loadingOp != null && !loadingOp.isDone)
            {
                yield return null;
            }
        }

        yield return null;

        // 2. Load target scene async
        var operation = SceneManager.LoadSceneAsync(sceneName, loadMode);
        if (operation == null)
        {
            BaseLog.LogError($"[GameSceneManager] Failed to start loading scene '{sceneName}'. Is it added to Build Settings?");
            if (hasLoadingScene)
            {
                SceneManager.UnloadSceneAsync(loadingSceneName);
            }
            isLoading = false;
            yield break;
        }

        operation.allowSceneActivation = false;

        float targetDuration = fakeLoadingDuration > 0f ? fakeLoadingDuration : defaultFakeLoadingDuration;

        stutterPointsCache.Clear();
        if (stutterCount > 0 && maxStutterDuration > 0f)
        {
            for (int i = 0; i < stutterCount; i++)
            {
                stutterPointsCache.Add(UnityEngine.Random.Range(0.15f, 0.85f));
            }
            stutterPointsCache.Sort();
        }

        float virtualProgress = 0f;
        float logicalTime = 0f;
        float currentStutterWait = 0f;
        int currentStutterIndex = 0;

        while (operation.progress < 0.89f || virtualProgress < 1f)
        {
            float dt = Time.unscaledDeltaTime;

            if (currentStutterIndex < stutterPointsCache.Count && virtualProgress >= stutterPointsCache[currentStutterIndex])
            {
                if (currentStutterWait <= 0f)
                {
                    currentStutterWait = UnityEngine.Random.Range(0.1f, maxStutterDuration);
                }

                currentStutterWait -= dt;
                if (currentStutterWait <= 0f)
                {
                    currentStutterIndex++;
                }
            }
            else
            {
                logicalTime += dt;
            }

            float fakeProgress = targetDuration > 0f ? Mathf.Clamp01(logicalTime / targetDuration) : 1f;
            float actualProgressNormalized = Mathf.Clamp01(operation.progress / 0.9f);

            float targetProgress = Mathf.Max(fakeProgress, actualProgressNormalized);

            if (operation.progress < 0.89f)
            {
                targetProgress = Mathf.Min(targetProgress, 0.99f);
            }

            virtualProgress = Mathf.MoveTowards(virtualProgress, targetProgress, dt * 3f);
            LoadProgressChanged?.Invoke(virtualProgress);

            if (virtualProgress >= 1f && operation.progress >= 0.89f)
            {
                break;
            }

            yield return null;
        }

        virtualProgress = 1f;
        LoadProgressChanged?.Invoke(virtualProgress);

        operation.allowSceneActivation = true;
        while (!operation.isDone)
        {
            yield return null;
        }

        var loadedScene = SceneManager.GetSceneByName(sceneName);
        if (loadedScene.IsValid() && loadedScene.isLoaded)
        {
            if (setActiveScene && loadedScene.IsValid() && SceneManager.GetActiveScene() != loadedScene)
            {
                SceneManager.SetActiveScene(loadedScene);
            }

            if (loadMode == LoadSceneMode.Single || setActiveScene)
            {
                activeSceneName = loadedScene.name;
            }

            onLoaded?.Invoke(loadedScene);
            SceneLoadCompleted?.Invoke(sceneName);
        }

        yield return Resources.UnloadUnusedAssets();

        // 3. Unload LoadingScene if it was loaded additively
        if (hasLoadingScene)
        {
            var unloadLoadingOp = SceneManager.UnloadSceneAsync(loadingSceneName);
            while (unloadLoadingOp != null && !unloadLoadingOp.isDone)
            {
                yield return null;
            }
        }

        isLoading = false;
    }

    private IEnumerator UnloadSceneRoutine(string sceneName, Action<string> onCompleted)
    {
        if (isLoading || string.IsNullOrWhiteSpace(sceneName))
        {
            yield break;
        }

        isLoading = true;

        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            isLoading = false;
            yield break;
        }

        var operation = SceneManager.UnloadSceneAsync(scene);
        while (operation != null && !operation.isDone)
        {
            yield return null;
        }

        if (string.Equals(activeSceneName, sceneName, StringComparison.OrdinalIgnoreCase))
        {
            activeSceneName = SceneManager.GetActiveScene().name;
        }

        onCompleted?.Invoke(sceneName);
        SceneUnloadCompleted?.Invoke(sceneName);
        isLoading = false;
    }
}
