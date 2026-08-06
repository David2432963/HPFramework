using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input action map controller for shared runtime input state managed by VContainer.
/// </summary>
public sealed class InputManager : MonoBehaviour
{
    private InputActionAsset inputActions;
    private string currentMapName;

    private void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// Reset the runtime input state for a fresh session.
    /// </summary>
    public void Initialize()
    {
        DisableCurrentMap();
        currentMapName = null;
    }

    /// <summary>
    /// Provide the input action asset used by this manager.
    /// </summary>
    public void SetInputActions(InputActionAsset asset)
    {
        if (ReferenceEquals(inputActions, asset))
        {
            return;
        }

        DisableCurrentMap();
        inputActions = asset;
        currentMapName = null;
    }

    /// <summary>
    /// Enable the named action map when it exists.
    /// </summary>
    public bool TryEnableMap(string mapName)
    {
        if (!InputActionMapFacade.EnableActionMap(inputActions, mapName))
        {
            return false;
        }

        currentMapName = mapName;
        return true;
    }

    /// <summary>
    /// Disable the named action map when it exists.
    /// </summary>
    public bool TryDisableMap(string mapName)
    {
        var wasCurrentMap = string.Equals(currentMapName, mapName);
        if (!InputActionMapFacade.DisableActionMap(inputActions, mapName))
        {
            return false;
        }

        if (wasCurrentMap)
        {
            currentMapName = null;
        }

        return true;
    }

    /// <summary>
    /// Disable the current map and switch to the requested one when it exists.
    /// </summary>
    public bool TrySwitchMap(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return false;
        }

        if (string.Equals(currentMapName, mapName))
        {
            return true;
        }

        var previousMapName = currentMapName;
        DisableCurrentMap();

        if (TryEnableMap(mapName))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(previousMapName))
        {
            TryEnableMap(previousMapName);
        }

        return false;
    }

    /// <summary>
    /// Try to get an action from the configured input asset.
    /// </summary>
    public bool TryGetAction(string mapName, string actionName, out InputAction action)
    {
        return InputActionMapFacade.TryGetAction(inputActions, mapName, actionName, out action);
    }

    /// <summary>
    /// Disable the current action map when one is active.
    /// </summary>
    public void DisableCurrentMap()
    {
        if (string.IsNullOrWhiteSpace(currentMapName))
        {
            return;
        }

        InputActionMapFacade.DisableActionMap(inputActions, currentMapName);
        currentMapName = null;
    }

    /// <summary>
    /// Get the currently enabled map name, if any.
    /// </summary>
    public string GetCurrentMapName()
    {
        return currentMapName;
    }
}
