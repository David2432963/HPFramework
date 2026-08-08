namespace HP.Framework.Persistence
{
    using System;
    using UnityEngine;
    using HP.Framework;

    /// <summary>
    /// PlayerPrefs-based save helper for small reusable data.
    /// This is best for simple serializable data, not for complex object graphs.
    /// </summary>
    public static class PlayerPrefSave
    {
        /// <summary>
        /// Save a serializable object into PlayerPrefs as JSON.
        /// </summary>
        public static bool Save<T>(string key, T data, string relativeFolder = null, bool flush = true)
        {
            try
            {
                PlayerPrefs.SetString(GetKey(key, relativeFolder), JsonUtility.ToJson(data));
                if (flush)
                {
                    PlayerPrefs.Save();
                }
                return true;
            }
            catch (Exception exception)
            {
                BaseLog.LogWarning($"[PlayerPrefSave] Khong the luu '{key}': {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// Try to load a serializable object from PlayerPrefs.
        /// </summary>
        public static bool TryLoad<T>(string key, out T data, string relativeFolder = null)
        {
            try
            {
                var storageKey = GetKey(key, relativeFolder);
                if (!PlayerPrefs.HasKey(storageKey))
                {
                    data = default;
                    return false;
                }

                var json = PlayerPrefs.GetString(storageKey);
                data = JsonUtility.FromJson<T>(json);
                return true;
            }
            catch (Exception exception)
            {
                BaseLog.LogWarning($"[PlayerPrefSave] Khong the doc '{key}': {exception.Message}");
                data = default;
                return false;
            }
        }

        public static T LoadOrDefault<T>(string key, T defaultValue = default, string relativeFolder = null)
        {
            return TryLoad(key, out T data, relativeFolder) ? data : defaultValue;
        }

        public static void SetString(string key, string value, string relativeFolder = null)
        {
            PlayerPrefs.SetString(GetKey(key, relativeFolder), value ?? string.Empty);
        }

        public static string GetString(string key, string defaultValue = "", string relativeFolder = null)
        {
            return PlayerPrefs.GetString(GetKey(key, relativeFolder), defaultValue);
        }

        public static void SetInt(string key, int value, string relativeFolder = null)
        {
            PlayerPrefs.SetInt(GetKey(key, relativeFolder), value);
        }

        public static int GetInt(string key, int defaultValue = 0, string relativeFolder = null)
        {
            return PlayerPrefs.GetInt(GetKey(key, relativeFolder), defaultValue);
        }

        public static void SetFloat(string key, float value, string relativeFolder = null)
        {
            PlayerPrefs.SetFloat(GetKey(key, relativeFolder), value);
        }

        public static float GetFloat(string key, float defaultValue = 0f, string relativeFolder = null)
        {
            return PlayerPrefs.GetFloat(GetKey(key, relativeFolder), defaultValue);
        }

        public static void SetBool(string key, bool value, string relativeFolder = null)
        {
            PlayerPrefs.SetInt(GetKey(key, relativeFolder), value ? 1 : 0);
        }

        public static bool GetBool(string key, bool defaultValue = false, string relativeFolder = null)
        {
            return PlayerPrefs.GetInt(GetKey(key, relativeFolder), defaultValue ? 1 : 0) == 1;
        }

        public static bool Exists(string key, string relativeFolder = null)
        {
            return PlayerPrefs.HasKey(GetKey(key, relativeFolder));
        }

        public static bool Delete(string key, string relativeFolder = null)
        {
            try
            {
                var storageKey = GetKey(key, relativeFolder);
                if (!PlayerPrefs.HasKey(storageKey))
                {
                    return false;
                }

                PlayerPrefs.DeleteKey(storageKey);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception exception)
            {
                BaseLog.LogWarning($"[PlayerPrefSave] Khong the xoa '{key}': {exception.Message}");
                return false;
            }
        }

        public static void DeleteAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }

        public static string GetKey(string key, string relativeFolder = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key must not be empty.", nameof(key));
            }

            if (string.IsNullOrWhiteSpace(relativeFolder))
            {
                return key;
            }

            return relativeFolder + BaseConstants.PlayerPrefSeparator + key;
        }
    }


}


