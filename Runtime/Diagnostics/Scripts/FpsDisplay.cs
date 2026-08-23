using System.Text;
using HP.Framework.Assets;
using HP.Framework.Audio;
using HP.Framework.Input;
using HP.Framework.Performance;
using HP.Framework.Pooling;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

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
        [SerializeField] private bool includeInReleaseBuild;

        private IPerformanceDiagnosticsControl performanceControl;
        private IPerformanceService performanceService;
        private IAssetDiagnostics assetDiagnostics;
        private IPoolDiagnostics poolDiagnostics;
        private IAudioDiagnostics audioDiagnostics;
        private IInputDiagnostics inputDiagnostics;
        private readonly StringBuilder diagnosticsText = new StringBuilder(384);
        private MobileDiagnosticsSampler sampler;
        private bool isFpsLocked;
        private int latestRawFps;
        private int lastDisplayedRawFps = -1;
        private string lastDiagnosticsText;

        [Inject]
        public void Construct(
            IPerformanceDiagnosticsControl performanceControl,
            IPerformanceService performanceService,
            IAssetDiagnostics assetDiagnostics,
            IPoolDiagnostics poolDiagnostics,
            IAudioDiagnostics audioDiagnostics,
            IInputDiagnostics inputDiagnostics)
        {
            this.performanceControl = performanceControl;
            this.performanceService = performanceService;
            this.assetDiagnostics = assetDiagnostics;
            this.poolDiagnostics = poolDiagnostics;
            this.audioDiagnostics = audioDiagnostics;
            this.inputDiagnostics = inputDiagnostics;
            RecreateSampler();
        }

        private void Awake()
        {
            if (!showFPS)
            {
                gameObject.SetActive(false);
                return;
            }

#if !UNITY_EDITOR
            if (!Debug.isDebugBuild && !includeInReleaseBuild)
            {
                gameObject.SetActive(false);
            }
#endif
        }

        private void Start()
        {
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
            RecreateSampler();
        }

        private void OnDisable()
        {
            UnbindButtons();
            DisposeSampler();
        }

        private void OnDestroy()
        {
            if (isFpsLocked)
            {
                RestoreOriginalFrameRate();
            }

            DisposeSampler();
        }

        private void Update()
        {
            float rawDelta = Time.unscaledDeltaTime;
            latestRawFps = rawDelta > 0f ? Mathf.RoundToInt(1f / rawDelta) : 0;
            if (sampler == null)
            {
                RecreateSampler();
            }

            if (sampler == null
                || !sampler.TrySample(
                    Time.unscaledTimeAsDouble,
                    rawDelta,
                    out MobileDiagnosticsSnapshot snapshot))
            {
                return;
            }

            string formatted = BuildDiagnosticsText(snapshot);
            if (txtFps != null && txtFps.enabled && formatted != lastDiagnosticsText)
            {
                lastDiagnosticsText = formatted;
                txtFps.text = formatted;
            }

            if (txtRawFps != null && txtRawFps.enabled && latestRawFps != lastDisplayedRawFps)
            {
                lastDisplayedRawFps = latestRawFps;
                txtRawFps.text = "FPS (Realtime): " + latestRawFps;
            }

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

            if (performanceControl == null)
            {
                Debug.LogWarning("[Diagnostics] FPS lock requires IPerformanceDiagnosticsControl injection.", this);
                return;
            }

            isFpsLocked = true;
            performanceControl.SetFrameRateOverride(targetFrameRate);
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
            performanceControl?.ClearFrameRateOverride();
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

            txtFrameRateLockState.text = isFpsLocked && performanceControl != null
                ? $"FPS LOCK {performanceControl.EffectiveTargetFrameRate}"
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

        private void RecreateSampler()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            DisposeSampler();
            sampler = new MobileDiagnosticsSampler(
                performanceService,
                assetDiagnostics,
                poolDiagnostics,
                audioDiagnostics,
                inputDiagnostics,
                new UnityDiagnosticsPlatformMetrics(),
                refreshInterval);
        }

        private void DisposeSampler()
        {
            sampler?.Dispose();
            sampler = null;
        }

        private string BuildDiagnosticsText(MobileDiagnosticsSnapshot snapshot)
        {
            diagnosticsText.Clear();
            diagnosticsText.Append("FPS ")
                .Append(Mathf.RoundToInt(snapshot.AverageFps))
                .Append(" | ")
                .Append(snapshot.FrameTimeMilliseconds.ToString("0.0"))
                .Append(" ms | target ")
                .Append(snapshot.TargetFrameRate)
                .Append(" | ")
                .Append(snapshot.PerformanceTier)
                .AppendLine();

            diagnosticsText.Append("CPU/GPU ");
            if (snapshot.FrameTimingAvailable)
            {
                diagnosticsText.Append(snapshot.CpuFrameMilliseconds.ToString("0.0"))
                    .Append('/')
                    .Append(snapshot.GpuFrameMilliseconds.ToString("0.0"))
                    .Append(" ms");
            }
            else
            {
                diagnosticsText.Append("n/a");
            }

            diagnosticsText.Append(" | managed ")
                .Append((snapshot.ManagedMemoryBytes / (1024f * 1024f)).ToString("0.0"))
                .Append(" MB | GC ");
            if (snapshot.GcAllocatedAvailable)
            {
                diagnosticsText.Append(snapshot.GcAllocatedBytes / 1024f)
                    .Append(" KB");
            }
            else
            {
                diagnosticsText.Append("n/a");
            }

            diagnosticsText.AppendLine();
            AppendFrameworkStats(snapshot);
            return diagnosticsText.ToString();
        }

        private void AppendFrameworkStats(MobileDiagnosticsSnapshot snapshot)
        {
            if (snapshot.PoolStatsAvailable)
            {
                diagnosticsText.Append("Pool ")
                    .Append(snapshot.PoolStats.ActiveInstanceCount)
                    .Append(" active/")
                    .Append(snapshot.PoolStats.InactiveInstanceCount)
                    .Append(" idle");
            }
            else
            {
                diagnosticsText.Append("Pool n/a");
            }

            if (snapshot.AssetStatsAvailable)
            {
                diagnosticsText.Append(" | Asset ")
                    .Append(snapshot.AssetStats.LoadedAssetCount)
                    .Append(" loaded/")
                    .Append(snapshot.AssetStats.ActiveReferenceCount)
                    .Append(" refs");
            }
            else
            {
                diagnosticsText.Append(" | Asset n/a");
            }

            if (snapshot.AudioStatsAvailable)
            {
                diagnosticsText.Append(" | Audio ")
                    .Append(snapshot.AudioStats.ActiveSfxVoices)
                    .Append('/')
                    .Append(snapshot.AudioStats.MaxSfxVoices);
            }
            else
            {
                diagnosticsText.Append(" | Audio n/a");
            }

            if (snapshot.InputStatsAvailable)
            {
                diagnosticsText.Append(" | Input ")
                    .Append(string.IsNullOrWhiteSpace(snapshot.InputStats.PrimaryMapName)
                        ? "none"
                        : snapshot.InputStats.PrimaryMapName)
                    .Append(" +")
                    .Append(snapshot.InputStats.ActiveLeaseCount);
            }
            else
            {
                diagnosticsText.Append(" | Input n/a");
            }
        }
    }
}
