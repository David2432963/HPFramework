using System;
using UnityEngine;
using UnityEngine.UI;

public enum AdResult 
{ 
    Done, 
    Skipped, 
    Cancelled 
}

[DisallowMultipleComponent]
public sealed class SimulatedAds : MonoBehaviour
{
    public static SimulatedAds Instance { get; private set; }

    [Header("UI Elements")]
    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private Button skipCheatBtn;
    [SerializeField] private Button skipNormalBtn;
    [SerializeField] private Text timerText;

    [Header("Settings")]
    [SerializeField] private bool adsEnabled = true;
    [SerializeField, Min(0f)] private float skipDelay = 5f;

    private float timer;
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
        if (Instance != null && Instance != this)
        {
            Destroy(transform.root.gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);

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
        if (Instance == this)
        {
            Instance = null;
        }

        if (skipCheatBtn != null)
        {
            skipCheatBtn.onClick.RemoveListener(OnSkipCheat);
        }

        if (skipNormalBtn != null)
        {
            skipNormalBtn.onClick.RemoveListener(OnSkipNormal);
        }
    }

    private void Update()
    {
        if (!showingAd)
        {
            return;
        }

        if (timer > 0f)
        {
            timer -= Time.unscaledDeltaTime;
            if (timerText != null)
            {
                timerText.text = $"Ad running... {Mathf.CeilToInt(timer)}s";
            }

            if (timer <= 0f)
            {
                CompleteAd(AdResult.Done);
            }
        }
    }

    public void ShowInterstitial(Action<AdResult> callback)
    {
        if (!adsEnabled)
        {
            callback?.Invoke(AdResult.Cancelled);
            return;
        }

        ShowInterstitialInternal(callback);
    }

    public void ShowReward(Action<AdResult> callback)
    {
        if (!adsEnabled)
        {
            callback?.Invoke(AdResult.Cancelled);
            return;
        }

        ShowRewardInternal(callback);
    }

    private void ShowInterstitialInternal(Action<AdResult> callback)
    {
        currentCallback = callback;
        rewardPending = false;
        ShowOverlay();
        timer = skipDelay;
    }

    private void ShowRewardInternal(Action<AdResult> callback)
    {
        currentCallback = callback;
        rewardPending = true;
        ShowOverlay();
        timer = skipDelay;
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
        currentCallback?.Invoke(result);
        currentCallback = null;
    }

    private void OnSkipCheat()
    {
        CompleteAd(AdResult.Done);
    }

    private void OnSkipNormal()
    {
        if (rewardPending)
        {
            CompleteAd(AdResult.Cancelled);
        }
        else
        {
            CompleteAd(AdResult.Skipped);
        }
    }
}
