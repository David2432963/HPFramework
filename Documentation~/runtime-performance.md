# Runtime performance

HP Framework favors explicit ownership and predictable hot paths. The framework does not try to remove every allocation from convenience APIs; instead it provides non-alloc/cached paths for code that can run every frame or across many objects.

Use Unity Profiler data to decide when to replace convenience paths with the lower-level APIs documented here.

## Animator playback ticking

`AnimatorPlay` instances register with an internal framework tick registry only while a non-loop playback needs completion tracking or a completed playback is holding its final frame. Idle components and loop playback do not add per-object PlayerLoop work.

One application-owned dispatcher implements VContainer's `ITickable` in `RootLifetimeScope` and updates the registry. Runtime playback therefore requires the HP Framework Bootstrap to be built before `Play`, `CrossFade` or `PlayOneShot` is called. Dynamically spawned prefabs can register after the container is built and do not need to resolve a `LifetimeScope`.

Registration changes made from playback callbacks are deferred safely until the current dispatch ends. No per-frame collection snapshots are allocated after the registry collections have reached their required capacity.

## Pooling

`PoolRuntime` caches each pooled instance's `IPoolable[]` metadata when the instance is first created/prewarmed.

Spawn and release therefore reuse the cached component list instead of calling `GetComponentsInChildren` every time.

The expected contract is that an instance does not add/remove `IPoolable` components after entering the pool.

## Resources loading

`ResourcesAssetProvider` serializes same-key load requests. If multiple callers request the same key concurrently, one caller performs the Resources load and later callers reuse the cached result rather than issuing duplicate same-key loads.

The underlying Unity Resources APIs are still intended for Unity's main thread.

## Persistence

`JsonSaveFile` serializes save/load/delete operations per resolved file path so operations targeting the same save slot do not race over `.tmp`/`.bak` files.

Resolved paths are validated against `Application.persistentDataPath`; rooted/traversal-style relative folders and invalid filenames are rejected.

The per-path lock registry is intended for normal game save-slot counts. Do not generate unbounded unique save paths every frame.

## Physics queries

Physics helpers expose non-alloc overloads for hot paths, including caller-owned hit/collider buffers. Prefer those overloads for repeated queries such as AI detection, overlap checks or screen-ray interactions.

Keep allocating convenience overloads for low-frequency gameplay/editor code where readability matters more than a small allocation.

## Camera frustum queries

`GeometryUtility.CalculateFrustumPlanes(camera)` returns a new plane array. For bulk visibility work, cache one six-plane buffer and refill it once per camera update:

```csharp
private readonly Plane[] frustumPlanes = CameraUtils.CreateFrustumPlaneBuffer();

private void LateUpdate()
{
    if (!CameraUtils.CalculateFrustumPlanesNonAlloc(camera, frustumPlanes))
    {
        return;
    }

    foreach (Renderer target in targets)
    {
        if (CameraUtils.IsObjectVisible(target, frustumPlanes))
        {
            // Visible to this camera frustum.
        }
    }
}
```

Related APIs:

```csharp
CameraUtils.FrustumPlaneCount
CameraUtils.CreateFrustumPlaneBuffer()
CameraUtils.CalculateFrustumPlanesNonAlloc(camera, planes)
CameraUtils.IsBoundsVisible(bounds, planes)
CameraUtils.IsObjectVisible(renderer, planes)
CameraUtils.IsTargetVisible(transform, planes)
```

The existing camera-based convenience overloads remain available and use an internal reusable buffer, so they no longer allocate a new `Plane[]` for every call. However, they still recalculate the frustum each call.

For hundreds of objects, calculate the planes once and use the caller-owned-buffer overloads. For multiple cameras, keep one buffer per camera/context.

## Ripple Surface GPU clock

`RippleSurfaceController` does not push effect time through its `MaterialPropertyBlock` every frame. It stores a base effect time, a Unity-time reference and an active time scale; the shader derives the current effect time from Unity's shader clock.

CPU/MPB updates occur when playback state/ripple data changes. A lightweight scale check remains so runtime changes to the surface transform scale can update ripple axis scaling only when necessary.

Pause, resume, time-scale changes and restart preserve the logical effect clock.

## Bubbling Surface GPU clock

Normal scaled-time `BubblingSurfaceController` playback uses the same base/reference/scale clock pattern:

```text
CPU state change
    ↓
_EffectTime
_EffectTimeReference
_EffectTimeScale
    ↓
shader _Time.y advances the effect
```

This removes the previous per-frame `_EffectTime` MaterialPropertyBlock write for ordinary runtime playback.

Important behavior:

- scaled runtime playback uses the GPU shader clock
- pause/resume and time-scale changes capture/rebase the logical effect time
- disabling the controller freezes the shader clock for that renderer
- `RestartAnimation()` rebases the effect to zero
- `useUnscaledTime` uses the CPU fallback because Unity's normal shader `_Time` does not provide the required unscaled-time behavior
- edit-mode preview also uses the CPU fallback

The CPU fallback updates only the clock properties needed to preserve those behaviors.

## Safe Area and Diagnostics

Safe Area polling is throttled rather than running the full refresh path every frame, while rect-transform dimension changes can still trigger an immediate refresh.

Diagnostics UI updates displayed values at reduced intervals and avoids assigning identical text every frame where practical.

## UI rendering

The default Bootstrap keeps one root `UICanvas` with `ScreenRoot`, `PopupRoot`, `NotificationRoot` and `InputBlocker` mount points.

Do not split these into separate Canvas components purely for hierarchy organization. Additional Canvas boundaries can reduce rebuild scope but can also increase batching/sorting complexity. Split only when Profiler data shows Canvas rebuilds are a real bottleneck.

The default UI camera disables HDR, MSAA, dynamic resolution and occlusion culling because those features are unnecessary for the standard persistent UI camera path.

## Logging

`BaseLog` methods use conditional compilation. When `ENABLE_BASE_LOG` is not defined, conditional call sites (including their argument evaluation) are omitted by the compiler.

## Profiling rule

Prefer these priorities:

1. remove repeated hierarchy/component scans from hot paths
2. reuse caller-owned buffers for bulk queries
3. avoid per-object MaterialPropertyBlock writes when the shader can derive time/state itself
4. reduce unnecessary UI/layout/text refreshes
5. optimize string/reflection/general-purpose convenience utilities only after profiling shows they matter

Do not sacrifice correct ownership or clear APIs for micro-optimizations that are not visible in the Profiler.
