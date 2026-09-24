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
        [SerializeField] private Image presentationImage;
        [SerializeField] private Sprite loadingSprite;
        [SerializeField] private Sprite readySprite;
        [SerializeField] private GameObject loadingVisualRoot;
        [SerializeField] private GameObject readyVisualRoot;
        [SerializeField] private Image progressFillImage;
        [SerializeField] private RectTransform progressTrack;
        [SerializeField] private RectTransform progressIndicator;
        [SerializeField] private Button continueButton;

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
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueClicked);
                continueButton.onClick.AddListener(OnContinueClicked);
            }

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
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueClicked);
            }

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

            RefreshPresentation();
            RefreshText();
        }

        private void OnSceneLoadFailed(string sceneName, Exception exception)
        {
            currentStage = SceneLoadStage.Failed;
            loadFailed = true;
            RefreshPresentation();
            RefreshText();
        }

        private void SetProgress(float progress)
        {
            currentProgress = Mathf.Clamp01(progress);
            if (progressBar != null)
            {
                progressBar.value = currentProgress;
            }

            UpdateProgressVisual();
            RefreshPresentation();
            RefreshText();
        }

        private void RefreshPresentation()
        {
            bool canContinue = !loadFailed && currentStage == SceneLoadStage.WaitingForConfirmation;
            bool usesStateRoots = loadingVisualRoot != null || readyVisualRoot != null;

            if (loadingVisualRoot != null)
            {
                loadingVisualRoot.SetActive(!canContinue);
            }

            if (readyVisualRoot != null)
            {
                readyVisualRoot.SetActive(canContinue);
            }

            // Backward-compatible fallback for projects that still author one image
            // and swap its sprite instead of using composited loading/ready roots.
            if (!usesStateRoots && presentationImage != null)
            {
                Sprite targetSprite = canContinue && readySprite != null
                    ? readySprite
                    : loadingSprite;
                if (targetSprite != null)
                {
                    presentationImage.sprite = targetSprite;
                }
            }

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(canContinue);
                continueButton.interactable = canContinue;
            }
        }

        private void UpdateProgressVisual()
        {
            if (progressFillImage != null)
            {
                progressFillImage.fillAmount = currentProgress;
            }

            if (progressTrack == null || progressIndicator == null)
            {
                return;
            }

            float normalizedWidth = progressIndicator.anchorMax.x - progressIndicator.anchorMin.x;
            if (normalizedWidth > 0.0001f)
            {
                float halfWidth = normalizedWidth * 0.5f;
                Vector2 min = progressIndicator.anchorMin;
                Vector2 max = progressIndicator.anchorMax;
                min.x = currentProgress - halfWidth;
                max.x = currentProgress + halfWidth;
                progressIndicator.anchorMin = min;
                progressIndicator.anchorMax = max;
                return;
            }

            Vector2 position = progressIndicator.anchoredPosition;
            position.x = Mathf.Lerp(0f, progressTrack.rect.width, currentProgress);
            progressIndicator.anchoredPosition = position;
        }

        private void OnContinueClicked()
        {
            if (gameSceneManager == null || !gameSceneManager.ConfirmCurrentTransition())
            {
                return;
            }

            if (continueButton != null)
            {
                continueButton.interactable = false;
            }
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
                case SceneLoadStage.WaitingForConfirmation:
                    return "Ready";
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
