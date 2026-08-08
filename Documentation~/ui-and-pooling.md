# Scoped UI and pooling

The root Bootstrap provides persistent global `IUIService` and `IPoolService` implementations.

When a scene-owned prefab needs scene dependencies, shadow those services in the child scope:

```csharp
protected override void RegisterServices(IContainerBuilder builder)
{
    builder.RegisterScopedPool();
    builder.RegisterScopedUI();
}
```

`ScopedPoolService` instantiates with the child resolver and disposes its pool with the scope. `ScopedUIService` uses the persistent canvas roots but instantiates local screens/popups with the child resolver and destroys local views when the scope ends.

Pool instances cache their `IPoolable` components when the instance is first created. Spawn/release callbacks therefore do not rescan the complete hierarchy every time. The default contract assumes the set of `IPoolable` components does not change after the object has entered the pool.

## BasePopup animation preview

Assign a `UIAnimationPresetSO` to a `BasePopup` to enable edit-mode preview in the custom Inspector. Available controls include:

- Preview Show
- Preview Hide
- Preview Show → Hide
- Stop
- Reset
- Show/Hide progress scrubbing
- Preview speed
- Loop
- Auto Preview On Preset Change

Editor preview and runtime both use `PopupTransitionPlayer`, so delay, duration, easing, custom curves, backdrop fade, content scale, and content fade use the same evaluator. Preview changes are temporary and are restored when the Inspector closes, scripts reload, Play Mode begins, or Reset is pressed.

Global `UIManager.ClearScreens()` and `ClearPopups()` destroy/release owned views before clearing tracking collections, preventing orphaned UI instances.
