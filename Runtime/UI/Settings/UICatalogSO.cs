using System;
using System.Collections.Generic;
using UnityEngine;

namespace HP.Framework.UI
{
    public enum UIAssetMode
    {
        DirectPrefab = 0,
        AssetKey = 1
    }

    [CreateAssetMenu(fileName = "UICatalogSO", menuName = "HP Framework/UI/UI Catalog")]
    public sealed class UICatalogSO : ScriptableObject
    {
        [Header("Registered Popup Prefabs")]
        [SerializeField] private List<UIEntry> popupEntries = new List<UIEntry>();

        [Header("Registered Screen Prefabs")]
        [SerializeField] private List<UIEntry> screenEntries = new List<UIEntry>();

        public IReadOnlyList<UIEntry> PopupEntries => popupEntries;
        public IReadOnlyList<UIEntry> ScreenEntries => screenEntries;

        public bool TryGetPopupEntry(Type uiType, out UIEntry entry)
        {
            return TryGetEntry(uiType, popupEntries, out entry);
        }

        public bool TryGetScreenEntry(Type uiType, out UIEntry entry)
        {
            return TryGetEntry(uiType, screenEntries, out entry);
        }

        public bool TryGetPopupEntry<T>(out UIEntry entry) where T : BasePopup
        {
            return TryGetPopupEntry(typeof(T), out entry);
        }

        public bool TryGetScreenEntry<T>(out UIEntry entry) where T : BaseScreen
        {
            return TryGetScreenEntry(typeof(T), out entry);
        }

        public IEnumerable<UIEntry> GetPopupEntries()
        {
            return popupEntries;
        }

        public IEnumerable<UIEntry> GetScreenEntries()
        {
            return screenEntries;
        }

        public IEnumerable<UIEntry> GetPreloadEntries()
        {
            for (int i = 0; i < popupEntries.Count; i++)
            {
                UIEntry entry = popupEntries[i];
                if (entry != null && entry.PreloadOnBoot)
                {
                    yield return entry;
                }
            }

            for (int i = 0; i < screenEntries.Count; i++)
            {
                UIEntry entry = screenEntries[i];
                if (entry != null && entry.PreloadOnBoot)
                {
                    yield return entry;
                }
            }
        }

        public bool TryValidate(out string errorMessage)
        {
            var errors = new List<string>();
            ValidateEntryData(popupEntries, typeof(BasePopup), "Popup", errors);
            ValidateEntryData(screenEntries, typeof(BaseScreen), "Screen", errors);
            errorMessage = string.Join("\n", errors);
            return errors.Count == 0;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!TryValidate(out string errorMessage))
            {
                Debug.LogWarning(errorMessage, this);
            }

            ValidateEntries(popupEntries, typeof(BasePopup), "Popup");
            ValidateEntries(screenEntries, typeof(BaseScreen), "Screen");
        }

        private void ValidateEntries(List<UIEntry> entries, Type expectedType, string label)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                UIEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                Type entryType;
                if (!entry.TryGetRuntimeType(out entryType))
                {
                    Debug.LogWarning($"{nameof(UICatalogSO)} on {name} has an entry with no valid prefab or UI component.", this);
                    continue;
                }

                GameObject prefab = entry.Prefab;
                if (prefab == null)
                {
                    continue;
                }

                var popup = prefab.GetComponent<BasePopup>();
                var screen = prefab.GetComponent<BaseScreen>();
                if (expectedType == typeof(BasePopup) && popup == null)
                {
                    Debug.LogWarning($"{nameof(UICatalogSO)} marks '{prefab.name}' in the {label} list, but the prefab does not use {nameof(BasePopup)}.", prefab);
                }

                if (expectedType == typeof(BaseScreen) && screen == null)
                {
                    Debug.LogWarning($"{nameof(UICatalogSO)} marks '{prefab.name}' in the {label} list, but the prefab does not use {nameof(BaseScreen)}.", prefab);
                }
            }
        }
#endif

        [Serializable]
        public sealed class UIEntry
        {
            [SerializeField] private UIAssetMode assetMode;
            [SerializeField] private GameObject prefab;
            [SerializeField] private string assetKey;
            [Tooltip("Assembly-qualified BasePopup/BaseScreen type. Required for AssetKey entries.")]
            [SerializeField] private string runtimeTypeName;
            [SerializeField] private bool preloadOnBoot;
            [SerializeField] private bool cacheAfterClose = true;

            public UIAssetMode AssetMode => assetMode;
            public GameObject Prefab => prefab;
            public string AssetKey => assetKey;
            public string RuntimeTypeName => runtimeTypeName;
            public bool PreloadOnBoot => preloadOnBoot;
            public bool CacheAfterClose => cacheAfterClose;

            public bool TryGetRuntimeType(out Type runtimeType)
            {
                runtimeType = null;

                if (assetMode == UIAssetMode.AssetKey)
                {
                    runtimeType = string.IsNullOrWhiteSpace(runtimeTypeName)
                        ? null
                        : Type.GetType(runtimeTypeName, throwOnError: false);
                    return runtimeType != null;
                }

                GameObject currentPrefab = prefab;
                if (currentPrefab == null)
                {
                    return false;
                }

                try
                {
                    var popup = currentPrefab.GetComponent<BasePopup>();
                    if (popup != null)
                    {
                        runtimeType = popup.GetType();
                        return true;
                    }

                    var screen = currentPrefab.GetComponent<BaseScreen>();
                    if (screen != null)
                    {
                        runtimeType = screen.GetType();
                        return true;
                    }

                    return false;
                }
                catch (MissingReferenceException)
                {
                    return false;
                }
            }
        }

        private static void ValidateEntryData(
            List<UIEntry> entries,
            Type expectedType,
            string label,
            List<string> errors)
        {
            var types = new HashSet<Type>();
            for (int i = 0; i < entries.Count; i++)
            {
                UIEntry entry = entries[i];
                if (entry == null)
                {
                    errors.Add($"{label} entry at index {i} is null.");
                    continue;
                }

                if (entry.AssetMode == UIAssetMode.DirectPrefab && entry.Prefab == null)
                {
                    errors.Add($"{label} entry at index {i} has no direct prefab.");
                    continue;
                }

                if (entry.AssetMode == UIAssetMode.AssetKey
                    && string.IsNullOrWhiteSpace(entry.AssetKey))
                {
                    errors.Add($"{label} AssetKey entry at index {i} has no asset key.");
                }

                if (!entry.TryGetRuntimeType(out Type runtimeType))
                {
                    errors.Add($"{label} entry at index {i} has no valid runtime type.");
                    continue;
                }

                if (!expectedType.IsAssignableFrom(runtimeType))
                {
                    errors.Add(
                        $"{label} entry '{runtimeType.FullName}' does not derive from {expectedType.Name}.");
                }

                if (!types.Add(runtimeType))
                {
                    errors.Add($"Duplicate {label.ToLowerInvariant()} type '{runtimeType.FullName}'.");
                }
            }
        }

        private static bool TryGetEntry(Type uiType, List<UIEntry> entries, out UIEntry entry)
        {
            entry = null;

            if (uiType == null || entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                UIEntry candidate = entries[i];
                if (candidate == null)
                {
                    continue;
                }

                Type entryType;
                if (!candidate.TryGetRuntimeType(out entryType))
                {
                    continue;
                }

                if (entryType == uiType)
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}

