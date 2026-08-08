using UnityEngine;
using UnityEngine.UI;

namespace HP.Framework.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class FpsDisplay : MonoBehaviour
    {
        [SerializeField] private Text txtFps;
        [SerializeField] private Text txtRawFps;
        [SerializeField] private Text txtFrameRateLockState;
        [SerializeField] private Button lock60Button;
        [SerializeField] private Button lock90Button;
        [SerializeField] private Button lock120Button;
        [SerializeField] private Button unlockButton;
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.2f;
        [SerializeField, Min(1)] private int defaultLockedTargetFrameRate = 60;
        [SerializeField] private bool showFPS = true;
        [SerializeField] private bool lockFpsOnStart;
        [SerializeField] private bool showFpsCounter = true;

        private int originalTargetFrameRate;
        private int originalVSyncCount;
        private bool isFpsLocked;
        private int frameCount;
        private float timeAccumulator;
        private int latestRawFps;
        private int lastDisplayedFps = -1;
        private int lastDisplayedRawFps = -1;

        private void Awake()
        {
            originalTargetFrameRate = Application.targetFrameRate;
            originalVSyncCount = QualitySettings.vSyncCount;

            if (!showFPS)
            {
                gameObject.SetActive(false);
                return;
            }

            if (lockFpsOnStart)
            {
                LockFps(defaultLockedTargetFrameRate);
            }
        }

        private void OnEnable()
        {
            BindButtons();
            ApplyFpsCounterVisibility();
            RefreshLockStateText();
        }

        private void OnDisable()
        {
            UnbindButtons();
        }

        private void OnDestroy()
        {
            if (isFpsLocked)
            {
                RestoreOriginalFrameRate();
            }
        }

        private void Update()
        {
            float rawDelta = Time.unscaledDeltaTime;
            latestRawFps = rawDelta > 0f ? Mathf.RoundToInt(1f / rawDelta) : 0;
            frameCount++;
            timeAccumulator += rawDelta;

            if (timeAccumulator < refreshInterval)
            {
                return;
            }

            int averageFps = timeAccumulator > 0f
                ? Mathf.RoundToInt(frameCount / timeAccumulator)
                : 0;

            if (txtFps != null && txtFps.enabled && averageFps != lastDisplayedFps)
            {
                lastDisplayedFps = averageFps;
                txtFps.text = "FPS (Average): " + averageFps;
            }

            if (txtRawFps != null && txtRawFps.enabled && latestRawFps != lastDisplayedRawFps)
            {
                lastDisplayedRawFps = latestRawFps;
                txtRawFps.text = "FPS (Realtime): " + latestRawFps;
            }

            frameCount = 0;
            timeAccumulator = 0f;
        }

        public void LockFps60() => LockFps(60);
        public void LockFps90() => LockFps(90);
        public void LockFps120() => LockFps(120);

        public void UnlockFps()
        {
            RestoreOriginalFrameRate();
            RefreshLockStateText();
        }

        public void LockFps(int targetFrameRate)
        {
            if (targetFrameRate <= 0)
            {
                UnlockFps();
                return;
            }

            isFpsLocked = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
            RefreshLockStateText();
        }

        public void SetFpsCounterVisible(bool isVisible)
        {
            showFpsCounter = isVisible;
            ApplyFpsCounterVisibility();
        }

        private void RestoreOriginalFrameRate()
        {
            isFpsLocked = false;
            QualitySettings.vSyncCount = originalVSyncCount;
            Application.targetFrameRate = originalTargetFrameRate;
        }

        private void BindButtons()
        {
            BindButton(lock60Button, LockFps60);
            BindButton(lock90Button, LockFps90);
            BindButton(lock120Button, LockFps120);
            BindButton(unlockButton, UnlockFps);
        }

        private void UnbindButtons()
        {
            UnbindButton(lock60Button, LockFps60);
            UnbindButton(lock90Button, LockFps90);
            UnbindButton(lock120Button, LockFps120);
            UnbindButton(unlockButton, UnlockFps);
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void UnbindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        private void RefreshLockStateText()
        {
            if (txtFrameRateLockState == null)
            {
                return;
            }

            txtFrameRateLockState.text = isFpsLocked
                ? $"FPS LOCK {Application.targetFrameRate}"
                : "FPS LOCK OFF";
        }

        private void ApplyFpsCounterVisibility()
        {
            if (txtFps != null)
            {
                txtFps.enabled = showFpsCounter;
            }

            if (txtRawFps != null)
            {
                txtRawFps.enabled = showFpsCounter;
            }
        }
    }
}
