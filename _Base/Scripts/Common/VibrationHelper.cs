using UnityEngine;

/// <summary>
/// Native vibration helper for Android and iOS.
/// Allows precise control over vibration duration on Android, and uses Handheld.Vibrate on iOS.
/// 
/// Note: On Android devices, you must register the vibrate permission in your AndroidManifest.xml:
/// <code>
/// &lt;uses-permission android:name="android.permission.VIBRATE"/&gt;
/// </code>
/// </summary>
public static class VibrationHelper
{
#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject vibrator;

    private static void InitAndroid()
    {
        if (vibrator != null)
        {
            return;
        }

        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
        }
    }
#endif

    /// <summary>
    /// Trigger vibration for a specified duration in milliseconds.
    /// </summary>
    public static void Vibrate(long milliseconds)
    {
        if (milliseconds <= 0)
        {
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        InitAndroid();
        if (vibrator != null)
        {
            using (AndroidJavaClass vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect"))
            {
                int defaultAmplitude = vibrationEffectClass.GetStatic<int>("DEFAULT_AMPLITUDE");
                using (AndroidJavaObject effect = vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, defaultAmplitude))
                {
                    vibrator.Call("vibrate", effect);
                }
            }
        }
#elif UNITY_IOS && !UNITY_EDITOR
        // Fallback for iOS simple vibration
        Handheld.Vibrate();
#else
        // Editor debug logging
        Debug.Log($"[VibrationHelper] Vibrate triggered for {milliseconds} ms.");
#endif
    }
}
