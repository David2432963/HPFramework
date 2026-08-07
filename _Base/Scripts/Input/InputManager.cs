using System;
using Base;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

/// <summary>
/// Shared Input System context controller. Only one action map is enabled at a time.
/// </summary>
public sealed class InputManager : MonoBehaviour, IInitializable, IDisposable
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string defaultActionMap;
    [SerializeField] private bool enableDefaultMapOnInitialize = true;

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

        inputActions?.Disable();
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

        DisableCurrentMap();
        inputActions?.Disable();
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

        if (!string.IsNullOrWhiteSpace(currentMapName)
            && !string.Equals(currentMapName, mapName, StringComparison.Ordinal))
        {
            DisableCurrentMap();
        }

        actionMap.Enable();
        currentMapName = mapName;
        return true;
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

    public void Dispose()
    {
        DisableCurrentMap();
        inputActions?.Disable();
        initialized = false;
    }
}
