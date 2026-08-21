# Assets and ownership

HP Framework keeps asset loading behind provider contracts so game code does not need to know whether content comes from `Resources`, Addressables, or another backend.

## Compatibility API vs ownership-aware API

The original v3 API remains available:

```csharp
IAssetProvider
    LoadAssetAsync<T>()
    InstantiateAsync()
    ReleaseAsset()
    ReleaseInstance()
```

M2 adds an ownership-aware surface without adding members to `IAssetProvider`, so existing custom provider implementations remain source-compatible:

```csharp
IAssetLeaseProvider : IAssetProvider
    AcquireAsync<T>()
    InstantiateLeaseAsync<T>()
```

The default `ResourcesAssetProvider` is registered as both interfaces.

Prefer the lease API for new code:

```csharp
IAssetLease<TextAsset> lease = await assets.AcquireAsync<TextAsset>("Config/GameBalance");
try
{
    Use(lease.Asset);
}
finally
{
    lease.Dispose();
}
```

Each successful acquire owns exactly one logical reference. Disposing the same lease more than once is safe and releases that reference only once.

## Ownership guarantee boundary

HP Framework can only account for resources acquired through the provider.

```text
provider Acquire/Load/Instantiate -> provider-owned lifecycle
Resources.Load/direct reference   -> external owner lifecycle
serialized reference              -> external owner lifecycle
```

`ReleaseAsset` and `ReleaseInstance` only release objects the provider knows it owns. They do not unload/destroy arbitrary external references passed to them.

## Asset identity: key + canonical runtime type

A key maps to one canonical runtime asset type while its provider record exists.

Compatible requests reuse the record. For example, a loaded `TextAsset` can also be requested as `UnityEngine.Object`.

Unrelated requests fail clearly instead of performing a second ambiguous load:

```csharp
await assets.AcquireAsync<TextAsset>("Config/GameBalance");
await assets.AcquireAsync<GameObject>("Config/GameBalance"); // fails
```

Concurrent compatible requests share the same underlying load.

## Shared async cancellation

Cancellation belongs to the waiter, not to the shared underlying Unity load.

```text
A waits
B waits
    -> one underlying load

A cancels
    -> A stops waiting
    -> B and the shared load continue
```

If every waiter cancels and the Unity operation cannot be cancelled, the operation may finish. No lease is created for cancelled waiters; the completed zero-reference record becomes trim-eligible.

Failed loads are removed from provider ownership so a later request can retry instead of inheriting a permanently faulted cache record.

## Logical release vs physical unload

`refCount == 0` means an asset is eligible for release; it does not mean every backend can physically unload it in the same way.

For the default Resources backend:

- released lease records remain cached at zero references until `TrimUnused()`;
- `TextAsset`, textures and other supported non-GameObject resources can use `Resources.UnloadAsset` when the provider trims/releases them;
- `GameObject` / `Component` assets are removed from provider ownership but are not passed to `Resources.UnloadAsset`;
- `Resources.UnloadUnusedAssets()` is never called automatically by the provider because it is a global maintenance operation that can hitch.

A future Addressables provider owns and releases Addressables handles instead. Core/Assets code must not assume the two backends have identical physical unload behavior.

## Low-memory behavior

Ownership-aware providers can implement `IAssetMemoryService`:

```csharp
int LoadedAssetCount { get; }
int UnusedAssetCount { get; }
void TrimUnused();
```

The default Bootstrap bridges `IApplicationLifecycle.LowMemory` to `TrimUnused()` through a Bootstrap composition adapter. The Lifecycle assembly does not reference Assets and the Assets assembly does not reference Lifecycle.

`TrimUnused()` removes only records with zero logical references and no active waiter/load. Active leases are never invalidated by a low-memory trim.

## Instance leases

`InstantiateLeaseAsync<T>()` returns an `IInstanceLease<T>` for component-based prefabs.

The instance lease owns both:

```text
spawned GameObject
+ backing asset lease
```

Disposing it destroys the spawned object exactly once and releases the backing asset reference. Provider disposal also force-cleans any still-owned instances.

The old `InstantiateAsync` / `ReleaseInstance` pair remains for compatibility.

## Scope-owned leases

`AssetLeaseScope` is an explicit collection helper for scene/feature ownership:

```csharp
private readonly AssetLeaseScope assets = new AssetLeaseScope();

public async UniTask LoadAsync(IAssetLeaseProvider provider)
{
    var config = assets.Track(await provider.AcquireAsync<TextAsset>("Config/GameBalance"));
    Use(config.Asset);
}

public void Dispose()
{
    assets.Dispose();
}
```

It is intentionally not a global registry. The scene/feature that owns the collection also owns its disposal boundary.

## Diagnostics

`IAssetDiagnostics.GetStats()` returns a small allocation-free snapshot containing:

```text
loaded assets
unused/zero-reference assets
active logical references
pending underlying loads
tracked instances
```

The full mobile diagnostics HUD consumes these counters later; the asset module does not create its own per-frame diagnostics loop.

## Addressables integration boundary

The base package does not currently hard-depend on Addressables. An Addressables implementation should live in an optional integration assembly/package and implement the same `IAssetLeaseProvider`, `IAssetMemoryService`, and `IAssetDiagnostics` contracts.

When Addressables is not installed, no Addressables assembly should be required for the base framework to compile.
