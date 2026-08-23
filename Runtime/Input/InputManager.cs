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
    public sealed class InputManager : MonoBehaviour, IInputDiagnostics, IInitializable, IDisposable
    {
        private sealed class InputMapLease : IInputMapLease
        {
            private InputManager owner;
            private readonly int generation;

            public InputMapLease(InputManager owner, string mapName, int generation)
            {
                this.owner = owner;
                MapName = mapName;
                this.generation = generation;
            }

            public string MapName { get; }
            public bool IsValid => owner != null && owner.IsLeaseValid(MapName, generation);

            public void Dispose()
            {
                InputManager currentOwner = owner;
                owner = null;
                currentOwner?.ReleaseLease(MapName, generation);
            }
        }

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string defaultActionMap;
        [SerializeField] private bool enableDefaultMapOnInitialize = true;

        private readonly HashSet<string> additionalMapNames =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> leasedMapRefCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private string currentMapName;
        private int leaseGeneration;
        private bool initialized;

        public InputActionAsset InputActions => inputActions;
        public string CurrentMapName => currentMapName;
        public InputServiceStats Stats
        {
            get
            {
                int activeLeaseCount = 0;
                foreach (int refCount in leasedMapRefCounts.Values)
                {
                    activeLeaseCount += refCount;
                }

                return new InputServiceStats(
                    currentMapName,
                    leasedMapRefCounts.Count,
                    activeLeaseCount,
                    additionalMapNames.Count);
            }
        }

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

            if (leasedMapRefCounts.ContainsKey(mapName))
            {
                return false;
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

        public bool TryDisableMap(string mapName)
        {
            if (IsAdditionalMapOwned(mapName)
                && !string.Equals(currentMapName, mapName, StringComparison.Ordinal))
            {
                return false;
            }

            if (!InputActionMapFacade.DisableActionMap(inputActions, mapName))
            {
                return false;
            }

            additionalMapNames.Remove(mapName);
            if (string.Equals(currentMapName, mapName, StringComparison.Ordinal))
            {
                currentMapName = null;
            }
            return true;
        }

        /// <summary>
        /// Enables an additional framework-owned map without replacing the primary map.
        /// Use this for one-owner compatibility scenarios such as gameplay + persistent UI.
        /// Multi-owner consumers should migrate to the lease API introduced by the later input hardening milestone.
        /// </summary>
        public bool TryEnableAdditionalMap(string mapName)
        {
            if (!InputActionMapFacade.TryGetActionMap(inputActions, mapName, out InputActionMap actionMap))
            {
                return false;
            }

            if (string.Equals(currentMapName, mapName, StringComparison.Ordinal))
            {
                return true;
            }

            actionMap.Enable();
            additionalMapNames.Add(mapName);
            return true;
        }

        /// <summary>
        /// Releases a tracked additional map. Releasing the current primary map is intentionally a no-op;
        /// primary ownership must be changed through the primary-map API.
        /// </summary>
        public bool TryDisableAdditionalMap(string mapName)
        {
            if (!InputActionMapFacade.TryGetActionMap(inputActions, mapName, out InputActionMap actionMap))
            {
                return false;
            }

            if (string.Equals(currentMapName, mapName, StringComparison.Ordinal))
            {
                return true;
            }

            if (!additionalMapNames.Remove(mapName))
            {
                // Do not disable a map owned by some external consumer. Treat an already-disabled
                // untracked map as the desired idempotent state; an enabled untracked map is an
                // ownership conflict that the caller must resolve explicitly.
                return !actionMap.enabled;
            }

            if (!leasedMapRefCounts.ContainsKey(mapName))
            {
                actionMap.Disable();
            }

            additionalMapNames.Remove(mapName);
            return true;
        }

        /// <summary>
        /// Acquires shared ownership of an additional action map. The map remains enabled until
        /// every lease and compatibility owner has released it.
        /// </summary>
        public IInputMapLease AcquireMap(string mapName)
        {
            if (string.IsNullOrWhiteSpace(mapName))
            {
                throw new ArgumentException("An input map name is required.", nameof(mapName));
            }

            if (string.Equals(currentMapName, mapName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Input map '{mapName}' is the current primary map and cannot be leased as an additional map.");
            }

            if (!InputActionMapFacade.TryGetActionMap(inputActions, mapName, out InputActionMap actionMap))
            {
                throw new InvalidOperationException(
                    $"Input map '{mapName}' was not found in the configured InputActionAsset.");
            }

            int refCount = leasedMapRefCounts.TryGetValue(mapName, out int currentRefCount)
                ? currentRefCount + 1
                : 1;
            leasedMapRefCounts[mapName] = refCount;
            actionMap.Enable();
            return new InputMapLease(this, mapName, leaseGeneration);
        }

        public bool IsMapEnabled(string mapName)
        {
            return InputActionMapFacade.TryGetActionMap(inputActions, mapName, out InputActionMap actionMap)
                && actionMap.enabled;
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

        public void Dispose()
        {
            DisableOwnedMaps();
            initialized = false;
        }

        private void DisableOwnedMaps()
        {
            DisableCurrentMap();

            if (inputActions != null)
            {
                foreach (string mapName in additionalMapNames)
                {
                    InputActionMapFacade.DisableActionMap(inputActions, mapName);
                }

                foreach (string mapName in leasedMapRefCounts.Keys)
                {
                    InputActionMapFacade.DisableActionMap(inputActions, mapName);
                }
            }

            additionalMapNames.Clear();
            leasedMapRefCounts.Clear();
            leaseGeneration++;
        }

        private bool IsAdditionalMapOwned(string mapName)
        {
            return additionalMapNames.Contains(mapName)
                || leasedMapRefCounts.ContainsKey(mapName);
        }

        private bool IsLeaseValid(string mapName, int generation)
        {
            return generation == leaseGeneration
                && leasedMapRefCounts.TryGetValue(mapName, out int refCount)
                && refCount > 0;
        }

        private void ReleaseLease(string mapName, int generation)
        {
            if (generation != leaseGeneration
                || !leasedMapRefCounts.TryGetValue(mapName, out int refCount))
            {
                return;
            }

            if (refCount > 1)
            {
                leasedMapRefCounts[mapName] = refCount - 1;
                return;
            }

            leasedMapRefCounts.Remove(mapName);
            if (!additionalMapNames.Contains(mapName)
                && !string.Equals(currentMapName, mapName, StringComparison.Ordinal))
            {
                InputActionMapFacade.DisableActionMap(inputActions, mapName);
            }
        }
    }


}
