using HP.Framework.Bootstrap;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HP.Framework.UI
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
        }

        private void OnEnable()
        {
            if (gameSceneManager != null)
            {
                gameSceneManager.LoadProgressChanged += OnProgressChanged;
            }
            if (progressBar != null) progressBar.value = 0f;
            if (progressText != null) progressText.text = "0%";
        }

        private void OnDisable()
        {
            if (gameSceneManager != null)
            {
                gameSceneManager.LoadProgressChanged -= OnProgressChanged;
            }
        }

        private void OnProgressChanged(float progress)
        {
            if (progressBar != null) progressBar.value = progress;
            if (progressText != null) progressText.text = $"{(progress * 100f):F0}%";
            BaseLog.Log($"[LoadingScreen] OnProgressChanged: {progress}");
        }
    }
}


