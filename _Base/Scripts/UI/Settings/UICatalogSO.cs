using System;
using System.Collections.Generic;
using UnityEngine;

namespace Base.UI
{
    [CreateAssetMenu(fileName = "UICatalogSO", menuName = "Base/UI/UI Catalog")]
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

#if UNITY_EDITOR
        private void OnValidate()
        {
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
            [SerializeField] private GameObject prefab;
            [SerializeField] private bool preloadOnBoot;
            [SerializeField] private bool cacheAfterClose = true;

            public GameObject Prefab => prefab;
            public bool PreloadOnBoot => preloadOnBoot;
            public bool CacheAfterClose => cacheAfterClose;

            public bool TryGetRuntimeType(out Type runtimeType)
            {
                runtimeType = null;

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
