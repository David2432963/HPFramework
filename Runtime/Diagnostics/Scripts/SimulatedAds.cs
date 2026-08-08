using System;
using UnityEngine;
using UnityEngine.UI;

namespace HP.Framework.Diagnostics
{
    public enum AdResult
    {
        Done,
        Skipped,
        Cancelled
    }

    [DisallowMultipleComponent]
    public sealed class SimulatedAds : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private Button skipCheatBtn;
        [SerializeField] private Button skipNormalBtn;
        [SerializeField] private Text timerText;

        [Header("Settings")]
        [SerializeField] private bool adsEnabled = true;
        [SerializeField, Min(0f)] private float skipDelay = 5f;

        private float timer;
        private int lastDisplayedSeconds = -1;
        private bool showingAd;
        private bool rewardPending;
        private Action<AdResult> currentCallback;

        public bool AdsEnabled
        {
            get => adsEnabled;
            set => adsEnabled = value;
        }

        public float SkipDelay
        {
            get => skipDelay;
            set => skipDelay = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            HideOverlay();

            if (skipCheatBtn != null)
            {
                skipCheatBtn.onClick.AddListener(OnSkipCheat);
            }

            if (skipNormalBtn != null)
            {
                skipNormalBtn.onClick.AddListener(OnSkipNormal);
            }
        }

        private void OnDestroy()
        {
            if (skipCheatBtn != null)
            {
                skipCheatBtn.onClick.RemoveListener(OnSkipCheat);
            }

            if (skipNormalBtn != null)
            {
                skipNormalBtn.onClick.RemoveListener(OnSkipNormal);
            }

            currentCallback = null;
        }

        private void Update()
        {
            if (!showingAd || timer <= 0f)
            {
                return;
            }

            timer -= Time.unscaledDeltaTime;
            RefreshTimerText();
            if (timer <= 0f)
            {
                CompleteAd(AdResult.Done);
            }
        }

        public void ShowInterstitial(Action<AdResult> callback)
        {
            if (!adsEnabled)
            {
                callback?.Invoke(AdResult.Cancelled);
                return;
            }

            ShowAd(callback, reward: false);
        }

        public void ShowReward(Action<AdResult> callback)
        {
            if (!adsEnabled)
            {
                callback?.Invoke(AdResult.Cancelled);
                return;
            }

            ShowAd(callback, reward: true);
        }

        private void ShowAd(Action<AdResult> callback, bool reward)
        {
            currentCallback = callback;
            rewardPending = reward;
            timer = skipDelay;
            lastDisplayedSeconds = -1;
            ShowOverlay();
            RefreshTimerText();

            if (timer <= 0f)
            {
                CompleteAd(AdResult.Done);
            }
        }

        private void RefreshTimerText()
        {
            if (timerText == null)
            {
                return;
            }

            int seconds = Mathf.Max(0, Mathf.CeilToInt(timer));
            if (seconds == lastDisplayedSeconds)
            {
                return;
            }

            lastDisplayedSeconds = seconds;
            timerText.text = $"Ad running... {seconds}s";
        }

        private void ShowOverlay()
        {
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(true);
            }

            if (timerText != null)
            {
                timerText.gameObject.SetActive(true);
            }

            showingAd = true;
        }

        private void HideOverlay()
        {
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }

            if (timerText != null)
            {
                timerText.gameObject.SetActive(false);
            }

            showingAd = false;
        }

        private void CompleteAd(AdResult result)
        {
            HideOverlay();
            Action<AdResult> callback = currentCallback;
            currentCallback = null;
            callback?.Invoke(result);
        }

        private void OnSkipCheat()
        {
            CompleteAd(AdResult.Done);
        }

        private void OnSkipNormal()
        {
            CompleteAd(rewardPending ? AdResult.Cancelled : AdResult.Skipped);
        }
    }
}
