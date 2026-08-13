using HP.Framework.Bootstrap;
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

        [Inject]
        public void Construct(GameSceneManager gameSceneManager)
        {
            this.gameSceneManager = gameSceneManager;
            this.gameSceneManager.LoadProgressChanged += OnProgressChanged;
            SetProgress(0f);
        }

        private void OnEnable()
        {
            SetProgress(0f);
        }

        private void OnDestroy()
        {
            if (gameSceneManager != null)
            {
                gameSceneManager.LoadProgressChanged -= OnProgressChanged;
            }
        }

        private void OnProgressChanged(float progress)
        {
            SetProgress(progress);
        }

        private void SetProgress(float progress)
        {
            float clampedProgress = Mathf.Clamp01(progress);
            if (progressBar != null)
            {
                progressBar.value = clampedProgress;
            }

            if (progressText != null)
            {
                progressText.text = $"{clampedProgress * 100f:F0}%";
            }
        }
    }
}
