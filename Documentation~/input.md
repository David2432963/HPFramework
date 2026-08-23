# Input map ownership

`InputManager` distinguishes one primary map from additional overlay maps.

- Primary maps use `TryEnableMap` / `TrySwitchMap` replacement semantics.
- Additional maps use `AcquireMap` and remain enabled until every owner releases its lease.
- `TryEnableAdditionalMap` / `TryDisableAdditionalMap` remain available for compatibility with a single known owner.

## Shared leases

```csharp
private IInputMapLease gameplayLease;

gameplayLease = inputManager.AcquireMap("Player");

// Release when the feature no longer owns the map.
gameplayLease.Dispose();
gameplayLease = null;
```

Acquiring the same map twice increments its ref-count. Disposing one lease leaves the map enabled for the other owner, and double-dispose is safe. Replacing the configured `InputActionAsset` or disposing `InputManager` invalidates all outstanding leases and disables every framework-owned map.

Leasing the current primary map is rejected with a descriptive exception. Switch primary ownership instead of mixing primary and overlay semantics for the same map.

`IInputDiagnostics.Stats` exposes the current primary map, distinct leased-map count, total active leases, and compatibility-owned map count without exposing mutable internals.
