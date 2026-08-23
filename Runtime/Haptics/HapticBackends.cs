using HP.Framework.Common;
using UnityEngine;

namespace HP.Framework.Haptics
{
    public static class HapticBackendFactory
    {
        public static IHapticBackend Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidHapticBackend();
#elif UNITY_IOS && !UNITY_EDITOR
            return new IOSHapticBackend();
#else
            return new FallbackHapticBackend();
#endif
        }
    }

    public sealed class FallbackHapticBackend : IHapticBackend
    {
        public bool IsAvailable => true;

        public void Play(HapticType type)
        {
            Vibrate(HapticPattern.GetDurationMilliseconds(type));
        }

        public void Vibrate(long milliseconds)
        {
            VibrationHelper.Vibrate(milliseconds);
        }

        public void Dispose()
        {
        }
    }

    public sealed class IOSHapticBackend : IHapticBackend
    {
        public bool IsAvailable => Application.platform == RuntimePlatform.IPhonePlayer;

        public void Play(HapticType type)
        {
            Vibrate(HapticPattern.GetDurationMilliseconds(type));
        }

        public void Vibrate(long milliseconds)
        {
            if (milliseconds > 0 && IsAvailable)
            {
                Handheld.Vibrate();
            }
        }

        public void Dispose()
        {
        }
    }

    public sealed class AndroidHapticBackend : IHapticBackend
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject vibrator;
        private AndroidJavaClass vibrationEffectClass;
        private int sdkVersion;
#endif

        public AndroidHapticBackend()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    vibrator = activity?.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }

                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    sdkVersion = version.GetStatic<int>("SDK_INT");
                }

                if (sdkVersion >= 26)
                {
                    vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[Haptics] Android vibrator is unavailable: {exception.Message}");
                Dispose();
            }
#endif
        }

        public bool IsAvailable
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    return vibrator != null && vibrator.Call<bool>("hasVibrator");
                }
                catch
                {
                    return false;
                }
#else
                return false;
#endif
            }
        }

        public void Play(HapticType type)
        {
            VibrateInternal(
                HapticPattern.GetDurationMilliseconds(type),
                HapticPattern.GetAndroidAmplitude(type));
        }

        public void Vibrate(long milliseconds)
        {
            VibrateInternal(milliseconds, -1);
        }

        public void Dispose()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            vibrationEffectClass?.Dispose();
            vibrationEffectClass = null;
            vibrator?.Dispose();
            vibrator = null;
#endif
        }

        private void VibrateInternal(long milliseconds, int amplitude)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (milliseconds <= 0 || !IsAvailable)
            {
                return;
            }

            try
            {
                if (sdkVersion >= 26 && vibrationEffectClass != null)
                {
                    int safeAmplitude = amplitude > 0
                        ? Mathf.Clamp(amplitude, 1, 255)
                        : vibrationEffectClass.GetStatic<int>("DEFAULT_AMPLITUDE");
                    using (AndroidJavaObject effect = vibrationEffectClass
                        .CallStatic<AndroidJavaObject>(
                            "createOneShot",
                            milliseconds,
                            safeAmplitude))
                    {
                        vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    vibrator.Call("vibrate", milliseconds);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[Haptics] Android vibration failed: {exception.Message}");
            }
#endif
        }
    }
}
