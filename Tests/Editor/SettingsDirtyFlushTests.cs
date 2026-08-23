using System;
using HP.Framework.Persistence;
using NUnit.Framework;

namespace HP.Framework.Tests
{
    public sealed class SettingsDirtyFlushTests
    {
        private sealed class FakeStore : ISettingsStore
        {
            public int SaveCount { get; private set; }
            public int SetCount { get; private set; }
            public bool ThrowOnSet { get; set; }

            public bool HasKey(string key, string section = null) => false;
            public bool GetBool(string key, bool defaultValue = false, string section = null) => defaultValue;
            public int GetInt(string key, int defaultValue = 0, string section = null) => defaultValue;
            public float GetFloat(string key, float defaultValue = 0f, string section = null) => defaultValue;
            public string GetString(string key, string defaultValue = "", string section = null) => defaultValue;
            public void SetBool(string key, bool value, string section = null) => MaybeThrow();
            public void SetInt(string key, int value, string section = null) => MaybeThrow();
            public void SetFloat(string key, float value, string section = null) => MaybeThrow();
            public void SetString(string key, string value, string section = null) => MaybeThrow();
            public bool Delete(string key, string section = null) => true;
            public void Save() => SaveCount++;

            private void MaybeThrow()
            {
                SetCount++;
                if (ThrowOnSet)
                {
                    throw new InvalidOperationException("persist failed");
                }
            }
        }

        [Test]
        public void SaveIfDirty_SavesOnce_AndClearsDirtyState()
        {
            var store = new FakeStore();
            var settings = new SettingsManager(store);
            settings.Initialize();

            Assert.That(settings.IsDirty, Is.False);
            settings.SoundEnabled = !settings.SoundEnabled;
            Assert.That(settings.IsDirty, Is.True);

            Assert.That(settings.SaveIfDirty(), Is.True);
            Assert.That(settings.IsDirty, Is.False);
            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(settings.SaveIfDirty(), Is.False);
            Assert.That(store.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void PersistFailure_DoesNotLoseDirtyState()
        {
            var store = new FakeStore();
            var settings = new SettingsManager(store);
            settings.Initialize();
            store.ThrowOnSet = true;

            Assert.Throws<InvalidOperationException>(() =>
                settings.SoundEnabled = !settings.SoundEnabled);
            Assert.That(settings.IsDirty, Is.True);

            store.ThrowOnSet = false;
            Assert.That(settings.SaveIfDirty(), Is.True);
            Assert.That(settings.IsDirty, Is.False);
            Assert.That(store.SaveCount, Is.EqualTo(1));
            Assert.That(store.SetCount, Is.GreaterThan(1), "Save should retry the cached snapshot after a failed Set.");
        }
    }
}
