# Troubleshooting

## Package compiles but the entry scene has no framework root

Place `Runtime/Bootstrap/Prefabs/Bootstrap.prefab` in the application entry scene. No generated Bootstrap is required.

Normal Repair does not overwrite existing UICamera/Canvas Scaler values, manager ownership, layout, catalogs, Input Actions or Toast assignments on a project prefab instance.

If you intentionally want to restore the framework-owned canonical hierarchy (`Audio`, `UI`, `Input`, `Scene`, `Pools`, `Haptics`) and stock defaults, use **Reset Canonical Bootstrap To Framework Defaults**.

## Bootstrap hierarchy still looks like the legacy layout

This can be expected after normal Repair because Repair is non-destructive and preserves existing ownership.

To intentionally migrate to the current canonical hierarchy, run **Reset Canonical Bootstrap To Framework Defaults**.

If a Bootstrap prefab stage is currently open and dirty in the Unity Editor, reconcile it before restoring the canonical asset. Project-specific values should live on prefab instances, not be applied to the framework prefab.

## Input System is not installed

The zero-setup canonical Bootstrap requires Input System and UGUI. Both are declared in `package.json`. Install the declared dependencies before using the canonical Bootstrap.

## Asset-key UI throws from a synchronous API

This is intentional. `ShowScreen<T>()` and `OpenPopup<T>()` support direct-prefab entries only. Use `ShowScreenAsync<T>()` or `OpenPopupAsync<T>()` for `AssetKey` entries so loading, cancellation, and lease ownership are explicit.

## Audio or diagnostics metric shows unavailable

Asset-key audio requires `IAssetLeaseProvider` from the root scope. Diagnostics renders unsupported `ProfilerRecorder`/`FrameTimingManager` counters and absent optional Input diagnostics as `n/a` rather than throwing.

## EventSystem input actions look empty in the prefab YAML

The canonical Bootstrap contains `InputSystemUIInputModule`. If its serialized action references are empty, Input System assigns its default UI actions on enable. HP Framework's `InputManager` separately references the tracked `BaseUIInputActions` asset.

## UI camera is not visible through the gameplay camera

Make sure the gameplay camera is tagged `MainCamera`.

When URP is present, HP Framework configures the gameplay camera as Base, the framework UICamera as Overlay, and attaches the UI camera to the Base camera stack without duplicate entries.

If the renderer does not expose a usable camera stack yet, the helper fails safely and can retry during later scene initialization/scene load.

The canonical Bootstrap itself does not serialize URP camera data; URP-specific components/configuration are added only when URP is available.

## Bootstrap/default settings are missing

They are canonical Git-tracked framework assets. Restore the missing files and `.meta` files from Git under:

```text
Assets/Plugins/HPFramework/Runtime/Bootstrap/Prefabs
Assets/Plugins/HPFramework/Runtime/Defaults
```

Do not regenerate replacement assets with new GUIDs on each machine.

## Duplicate VContainer or UniTask assemblies

HP Framework currently bundles both dependencies under `ThirdParty/`.

Do not keep a second source/package copy of the same assembly in the consuming project unless you deliberately restructure the dependency strategy; duplicate assemblies can produce type/assembly conflicts.

## Newtonsoft type conflicts

HP Framework bundles Json.NET as `HP.Framework.NewtonsoftJson.dll` with explicit-only assembly references from `HP.Framework.Extensions`.

Do not expose the framework's private `Newtonsoft.Json.*` types through public APIs that consumer assemblies are expected to share with another Newtonsoft build. Keep private Json.NET usage as an implementation detail.

## Test assemblies are unavailable

Framework tests are development contracts and are not required for normal game runtime use.

The current framework test assemblies are gated by their asmdef constraints/packages. Run them from a framework-development project that has the required test/input setup.

## Save/load path errors

`JsonSaveFile` resolves paths under `Application.persistentDataPath` and rejects traversal/rooted relative folders that would escape that root.

Concurrent operations targeting the same file are serialized so `.tmp`/`.bak` operations do not race.

## Duplicate Resources loads

`ResourcesAssetProvider` shares same-key compatible requests through one pending record. If a request for the same key uses an incompatible type, the provider fails descriptively instead of starting a duplicate typed load.

## Frustum checks are called for many objects

Do not recalculate frustum planes for every object.

Cache one caller-owned plane buffer and fill it once per camera update:

```csharp
Plane[] planes = CameraUtils.CreateFrustumPlaneBuffer();
CameraUtils.CalculateFrustumPlanesNonAlloc(camera, planes);

bool visible = CameraUtils.IsObjectVisible(renderer, planes);
```

See [Runtime performance](runtime-performance.md) for the bulk-query pattern.

## Bubbling Surface does not use the GPU clock

Normal scaled runtime playback uses the shader clock.

`useUnscaledTime` intentionally uses a CPU fallback because the standard Unity shader `_Time` value does not provide the required unscaled-time behavior. Edit-mode preview also uses the CPU fallback.

Pause/disable should freeze the effect state instead of letting the shader continue advancing independently.

## Safe Area

The Safe Area utility lives under `ThirdParty/SafeArea`.

It polls safe-area changes at a reduced interval instead of performing the full refresh every frame. Rect-transform dimension changes can trigger immediate refresh. If the component has no `RectTransform`, it disables itself and reports the configuration problem instead of destroying the GameObject.

The demo is under `Samples~/04 Safe Area`.

## Public redistribution

The framework does not currently declare final HP Framework license terms. Also verify the retained Safe Area helper's original redistribution terms before publishing that folder publicly.

See `THIRD PARTY NOTICES.md` for bundled dependency notices.

## Repository validation

From the framework root run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .github/scripts/Validate-Package.ps1
```
