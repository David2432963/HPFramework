namespace HP.Framework.Input
{
    using System;
    using System.Collections.Generic;
    using HP.Framework;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using VContainer.Unity;

    /// <summary>
    /// Shared Input System context controller. It owns one primary action map plus explicitly
    /// tracked additional maps without disabling unrelated maps in the same InputActionAsset.
    /// </summary>
    public sealed class InputManager : MonoBehaviour, IInitializable, IDisposable
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string defaultActionMap;
        [SerializeField] private bool enableDefaultMapOnInitialize = true;

        private readonly HashSet<string> additionalMapNames = new HashSet<string>(StringComparer.Ordinal);
        private string currentMapName;
        private bool initialized;

        public InputActionAsset InputActions => inputActions;
        public string CurrentMapName => currentMapName;

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            currentMapName = null;
            initialized = true;

            if (enableDefaultMapOnInitialize && !string.IsNullOrWhiteSpace(defaultActionMap))
            {
                if (!TryEnableMap(defaultActionMap))
                {
                    BaseLog.LogWarning(
                        $"[Input] Default action map '{defaultActionMap}' was not found.");
                }
            }
        }

        public void SetInputActions(InputActionAsset asset, string initialMap = null)
        {
            if (ReferenceEquals(inputActions, asset))
            {
                if (!string.IsNullOrWhiteSpace(initialMap))
                {
                    TrySwitchMap(initialMap);
                }
                return;
            }

            DisableOwnedMaps();
            inputActions = asset;
            currentMapName = null;

            if (!string.IsNullOrWhiteSpace(initialMap))
            {
                TryEnableMap(initialMap);
            }
        }

        public bool TryEnableMap(string mapName)
        {
            if (!InputActionMapFacade.TryGetActionMap(inputActions, mapName, out InputActionMap actionMap))
            {
                return false;
            }

            if (string.Equals(currentMapName, mapName, StringComparison.Ordinal))
            {
                additionalMapNames.Remove(mapName);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(currentMapName))
            {
                DisableCurrentMap();
            }

            additionalMapNames.Remove(mapName);
            actionMap.Enable();
            currentMapName = mapName;
            return true;
        }

        /// <summary>
        /// Enables an additional map without changing the current primary map. Additional maps are
        /// tracked by this manager and are disabled when the manager is disposed or its asset changes.
        /// </summary>
        public bool TryEnableAdditionalMap(string mapName)
        {
            if (string.IsNullOrWhiteSpace(mapName)
                || string.Equals(currentMapName, mapName, StringComparison.Ordinal))
            {
                return false;
            }

            if (!InputActionMapFacade.TryGetActionMap(inputActions, mapName, out InputActionMap actionMap))
            {
                return false;
            }

            actionMap.Enable();
            additionalMapNames.Add(mapName);
            return true;
        }

        /// <summary>
        /// Releases an additional map owned by this manager. Disabling an already released map is
        /// idempotent as long as that map exists and is not the current primary map.
        /// </summary>
        public bool TryDisableAdditionalMap(string mapName)
        {
            if (string.IsNullOrWhiteSpace(mapName)
                || string.Equals(currentMapName, mapName, StringComparison.Ordinal)
                || !InputActionMapFacade.TryGetActionMap(inputActions, mapName, out InputActionMap actionMap))
            {
                return false;
            }

            if (!additionalMapNames.Remove(mapName))
            {
                return true;
            }

            actionMap.Disable();
            return true;
        }

        public bool IsMapEnabled(string mapName)
        {
            return InputActionMapFacade.TryGetActionMap(inputActions, mapName, out InputActionMap actionMap)
                && actionMap.enabled;
        }

        public bool TryDisableMap(string mapName)
        {
            if (!InputActionMapFacade.DisableActionMap(inputActions, mapName))
            {
                return false;
            }

            if (string.Equals(currentMapName, mapName, StringComparison.Ordinal))
            {
                currentMapName = null;
            }

            additionalMapNames.Remove(mapName);
            return true;
        }

        public bool TrySwitchMap(string mapName)
        {
            if (string.IsNullOrWhiteSpace(mapName))
            {
                return false;
            }

            if (string.Equals(currentMapName, mapName, StringComparison.Ordinal))
            {
                return true;
            }

            string previous = currentMapName;
            DisableCurrentMap();
            if (TryEnableMap(mapName))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(previous))
            {
                TryEnableMap(previous);
            }
            return false;
        }

        public bool TryGetAction(string mapName, string actionName, out InputAction action)
        {
            return InputActionMapFacade.TryGetAction(
                inputActions,
                mapName,
                actionName,
                out action);
        }

        public void DisableCurrentMap()
        {
            if (string.IsNullOrWhiteSpace(currentMapName))
            {
                return;
            }

            InputActionMapFacade.DisableActionMap(inputActions, currentMapName);
            currentMapName = null;
        }

        public string GetCurrentMapName() => currentMapName;

        private void DisableOwnedMaps()
        {
            DisableCurrentMap();
            if (additionalMapNames.Count == 0)
            {
                return;
            }

            foreach (string mapName in additionalMapNames)
            {
                InputActionMapFacade.DisableActionMap(inputActions, mapName);
            }

            additionalMapNames.Clear();
        }

        public void Dispose()
        {
            DisableOwnedMaps();
            initialized = false;
        }
    }


}


