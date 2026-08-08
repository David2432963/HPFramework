namespace HP.Framework.Input
{
    using UnityEngine.InputSystem;

    /// <summary>
    /// Small facade over Input System action maps.
    /// Use this to keep map lookup and enable/disable logic in one reusable place.
    ///
    /// Example:
    /// <code>
    /// if (InputActionMapFacade.EnableActionMap(inputActions, "Player"))
    /// {
    ///     // Player map is now active.
    /// }
    /// </code>
    /// </summary>
    public static class InputActionMapFacade
    {
        /// <summary>
        /// Get an action map by name when it exists.
        /// </summary>
        public static bool TryGetActionMap(InputActionAsset asset, string mapName, out InputActionMap actionMap)
        {
            actionMap = null;

            if (asset == null || string.IsNullOrWhiteSpace(mapName))
            {
                return false;
            }

            actionMap = asset.FindActionMap(mapName, throwIfNotFound: false);
            return actionMap != null;
        }

        /// <summary>
        /// Get an action by map name and action name when both exist.
        /// </summary>
        public static bool TryGetAction(InputActionAsset asset, string mapName, string actionName, out InputAction action)
        {
            action = null;

            if (!TryGetActionMap(asset, mapName, out var actionMap) || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            action = actionMap.FindAction(actionName, throwIfNotFound: false);
            return action != null;
        }

        /// <summary>
        /// Enable a map when it exists.
        /// </summary>
        public static bool EnableActionMap(InputActionAsset asset, string mapName)
        {
            if (!TryGetActionMap(asset, mapName, out var actionMap))
            {
                return false;
            }

            actionMap.Enable();
            return true;
        }

        /// <summary>
        /// Disable a map when it exists.
        /// </summary>
        public static bool DisableActionMap(InputActionAsset asset, string mapName)
        {
            if (!TryGetActionMap(asset, mapName, out var actionMap))
            {
                return false;
            }

            actionMap.Disable();
            return true;
        }
    }


}


