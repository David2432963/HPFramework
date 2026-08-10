# UI and pooling

The root Bootstrap provides persistent application-wide `IUIService` and `IPoolService` implementations. Scene/feature scopes can shadow either service when local objects need the child resolver and child lifetime.

## Persistent UI hierarchy

The canonical UI domain is:

```text
UI                              UIManager
├── UICamera
├── UICanvas
│   ├── ScreenRoot
│   ├── PopupRoot
│   ├── NotificationRoot
│   └── InputBlocker
└── EventSystem
```

The four children under `UICanvas` are mount points, not separate Canvas components. Keeping a single root Canvas is the default baseline; split Canvas hierarchies only when profiling shows Canvas rebuilds are a real bottleneck.

`InputBlocker` is the full-screen raycast blocker previously represented by the legacy `LockCanvas` name.

When URP is present, `UICamera` is configured as Overlay and attached to the active `Camera.main` Base camera stack. The main gameplay camera must use the `MainCamera` tag.

## Scoped UI and pool services

When a scene-owned prefab needs scene dependencies, shadow the root services:

```csharp
protected override void RegisterServices(IContainerBuilder builder)
{
    builder.RegisterScopedPool();
    builder.RegisterScopedUI();
}
```

`ScopedPoolService` instantiates objects using the child resolver and disposes its pool with the owning scope.

`ScopedUIService` mounts local screens/popups under the persistent global UI roots, but creates them through the child resolver and destroys those local views when the scope ends.

This lets UI/pool prefabs receive scene/feature dependencies without moving the persistent canvas itself into every child scope.

## Pool runtime behavior

Pool instances cache their `IPoolable` component list when each instance is first created/prewarmed. Spawn/release callbacks therefore do not rescan the complete hierarchy every time.

The pool contract assumes the set of `IPoolable` components does not change after an instance has entered the pool. If a pooled object's component topology is modified dynamically, rebuild/recreate that pooled instance rather than relying on the old cached metadata.

Global pool runtime children are owned by the `Pools` domain in the canonical Bootstrap. Scoped pools create their own `ScopedPoolRoot` under the owning `LifetimeScope`.

## UI ownership and clearing

`UIManager.ClearScreens()` and `ClearPopups()` deactivate/destroy registered owned views before clearing the tracking collections. Reconfiguring the UI catalog also clears old owned views so previous instances do not become orphaned.

Scoped UI follows the same ownership rule: views created by a child scope are destroyed when that scope is disposed.

## BasePopup animation preview

Assign a `UIAnimationPresetSO` to a `BasePopup` to use the edit-mode preview controls in the custom Inspector:

- Preview Show
- Preview Hide
- Preview Show → Hide
- Stop
- Reset
- Show/Hide progress scrubbing
- Preview speed
- Loop
- Auto Preview On Preset Change

Editor preview and runtime both use `PopupTransitionPlayer`, so delays, duration, easing, custom curves, backdrop fade, content scale and content fade share the same evaluator.

Preview changes are temporary and are restored when the Inspector closes, scripts reload, Play Mode begins or Reset is pressed.

If DOTween is installed and its setup utility has enabled the UI module, runtime playback uses the optional bridge for popup, toast and button scale motion. The bridge caches the reflected DOTween API once so the framework keeps no hard package dependency. When DOTween or its UI module is absent, the same UI components fall back to the built-in coroutine and easing path.

## EventSystem and optional Input System

The reusable Bootstrap contains the EventSystem ownership point, while Setup/Reset configures the project-appropriate input module.

When Input System is available, HP Framework can configure `InputSystemUIInputModule` without making the reusable template serialize that optional package component. This keeps editable development mode resilient when `com.unity.inputsystem` is absent.
