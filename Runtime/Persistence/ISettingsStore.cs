namespace HP.Framework.Persistence
{
    /// <summary>
    /// Generic key/value persistence boundary for application settings. Domain-specific settings
    /// services should depend on this contract instead of reading PlayerPrefs directly.
    /// </summary>
    public interface ISettingsStore
    {
        bool HasKey(string key, string section = null);

        bool GetBool(string key, bool defaultValue = false, string section = null);
        int GetInt(string key, int defaultValue = 0, string section = null);
        float GetFloat(string key, float defaultValue = 0f, string section = null);
        string GetString(string key, string defaultValue = "", string section = null);

        void SetBool(string key, bool value, string section = null);
        void SetInt(string key, int value, string section = null);
        void SetFloat(string key, float value, string section = null);
        void SetString(string key, string value, string section = null);

        bool Delete(string key, string section = null);
        void Save();
    }

    /// <summary>
    /// Default ISettingsStore backed by Unity PlayerPrefs.
    /// </summary>
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        public bool HasKey(string key, string section = null)
            => PlayerPrefSave.Exists(key, section);

        public bool GetBool(string key, bool defaultValue = false, string section = null)
            => PlayerPrefSave.GetBool(key, defaultValue, section);

        public int GetInt(string key, int defaultValue = 0, string section = null)
            => PlayerPrefSave.GetInt(key, defaultValue, section);

        public float GetFloat(string key, float defaultValue = 0f, string section = null)
            => PlayerPrefSave.GetFloat(key, defaultValue, section);

        public string GetString(string key, string defaultValue = "", string section = null)
            => PlayerPrefSave.GetString(key, defaultValue, section);

        public void SetBool(string key, bool value, string section = null)
            => PlayerPrefSave.SetBool(key, value, section);

        public void SetInt(string key, int value, string section = null)
            => PlayerPrefSave.SetInt(key, value, section);

        public void SetFloat(string key, float value, string section = null)
            => PlayerPrefSave.SetFloat(key, value, section);

        public void SetString(string key, string value, string section = null)
            => PlayerPrefSave.SetString(key, value, section);

        public bool Delete(string key, string section = null)
            => PlayerPrefSave.Delete(key, section);

        public void Save()
            => PlayerPrefSave.Save();
    }
}


