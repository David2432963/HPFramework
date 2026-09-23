using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HP.Framework.Bootstrap.Loading
{
    /// <summary>
    /// Loading screen view component in LoadingScene.
    /// Injected by VContainer with shared GameSceneManager.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Slider progressBar;
        [SerializeField] private Text progressText;

        private GameSceneManager gameSceneManager;
        private float currentProgress;
        private SceneLoadStage currentStage;
        private bool loadFailed;
        private GameObject persistentRoot;

        [Inject]
        public void Construct(GameSceneManager gameSceneManager)
        {
            this.gameSceneManager = gameSceneManager;
            this.gameSceneManager.LoadProgressChanged += OnProgressChanged;
            this.gameSceneManager.LoadStageChanged += OnLoadStageChanged;
            this.gameSceneManager.SceneLoadFailed += OnSceneLoadFailed;
            currentStage = gameSceneManager.CurrentLoadStage;
            loadFailed = false;
            persistentRoot = transform.root.gameObject;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(persistentRoot);
            }

            SetProgress(0f);
        }

        private void OnEnable()
        {
            loadFailed = false;
            currentStage = gameSceneManager != null
                ? gameSceneManager.CurrentLoadStage
                : SceneLoadStage.Idle;
            SetProgress(0f);
        }

        private void OnDestroy()
        {
            if (gameSceneManager == null)
            {
                return;
            }

            gameSceneManager.LoadProgressChanged -= OnProgressChanged;
            gameSceneManager.LoadStageChanged -= OnLoadStageChanged;
            gameSceneManager.SceneLoadFailed -= OnSceneLoadFailed;
        }

        private void OnProgressChanged(float progress)
        {
            SetProgress(progress);
        }

        private void OnLoadStageChanged(SceneLoadStage stage)
        {
            currentStage = stage;
            if (stage == SceneLoadStage.Completed
                || (stage == SceneLoadStage.Idle && loadFailed))
            {
                DestroyPresentation();
                return;
            }

            if (stage != SceneLoadStage.Failed)
            {
                loadFailed = false;
            }

            RefreshText();
        }

        private void OnSceneLoadFailed(string sceneName, Exception exception)
        {
            currentStage = SceneLoadStage.Failed;
            loadFailed = true;
            RefreshText();
        }

        private void SetProgress(float progress)
        {
            currentProgress = Mathf.Clamp01(progress);
            if (progressBar != null)
            {
                progressBar.value = currentProgress;
            }

            RefreshText();
        }

        private void RefreshText()
        {
            if (progressText == null)
            {
                return;
            }

            if (loadFailed)
            {
                progressText.text = "Loading failed";
                return;
            }

            string percentage = $"{currentProgress * 100f:F0}%";
            string status = GetStatusText(currentStage);
            progressText.text = string.IsNullOrEmpty(status)
                ? percentage
                : $"{percentage}  {status}";
        }

        private void DestroyPresentation()
        {
            if (!Application.isPlaying || persistentRoot == null)
            {
                return;
            }

            Destroy(persistentRoot);
            persistentRoot = null;
        }

        private static string GetStatusText(SceneLoadStage stage)
        {
            switch (stage)
            {
                case SceneLoadStage.LoadingPresentation:
                    return "Preparing...";
                case SceneLoadStage.StreamingTarget:
                    return "Loading scene...";
                case SceneLoadStage.AwaitingActivation:
                case SceneLoadStage.ActivatingTarget:
                    return "Activating scene...";
                case SceneLoadStage.FinalizingTarget:
                    return "Finalizing scene...";
                case SceneLoadStage.WaitingForReadiness:
                    return "Preparing gameplay...";
                case SceneLoadStage.UnloadingPresentation:
                    return "Starting gameplay...";
                case SceneLoadStage.Completed:
                    return "Ready";
                case SceneLoadStage.Failed:
                    return "Loading failed";
                default:
                    return string.Empty;
            }
        }
    }
}
