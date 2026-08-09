# Troubleshooting

## Package compiles but the project is not configured

Open `Tools > HP Framework > Setup` and run **Setup / Repair Missing References**.

Normal Repair does not overwrite existing UICamera/Canvas Scaler values, manager ownership, layout, catalogs, Input Actions or Toast assignments.

If you intentionally want the canonical domain-owned Bootstrap hierarchy (`Audio`, `UI`, `Input`, `Scene`, `Pools`, `Haptics`) and stock defaults, use **Reset Bootstrap To Framework Defaults**.

## Bootstrap hierarchy still looks like the legacy layout

This can be expected after normal Repair because Repair is non-destructive and preserves existing ownership.

To intentionally migrate to the current canonical hierarchy, run **Reset Bootstrap To Framework Defaults**.

If a Bootstrap prefab stage is currently open and dirty in the Unity Editor, do not blindly save it over a newly generated/migrated prefab. Close/reconcile that stage first so stale prefab-stage contents do not overwrite the on-disk Bootstrap.

## Input System is not installed

In editable `Assets/Plugins` development mode, `HP.Framework.Input` is optional and is excluded when `com.unity.inputsystem` is absent.

Install Input System when the project needs `InputManager`. The rest of the framework should not fail compilation solely because that optional module is missing.

UGUI remains required by the current framework UI stack.

## EventSystem has no input module in the reusable template

This is intentional. The reusable Bootstrap template keeps optional package components out of the serialized template where required for import safety.

Setup/Reset configures the project-appropriate EventSystem input module when generating/repairing the project Bootstrap.

## UI camera is not visible through the gameplay camera

Make sure the gameplay camera is tagged `MainCamera`.

When URP is present, HP Framework configures the gameplay camera as Base, the framework UICamera as Overlay, and attaches the UI camera to the Base camera stack without duplicate entries.

If the renderer does not expose a usable camera stack yet, the helper fails safely and can retry during later scene initialization/scene load.

The reusable template itself does not need to serialize URP camera data; URP-specific components/configuration are added only when URP is available.

## Bootstrap/settings are missing

Run Setup again. It recreates/repairs project outputs under:

```text
Assets/Plugins/HPFramework/Generated
Assets/Plugins/HPFramework/Settings
```

The canonical reusable Bootstrap source remains at `Editor/Templates/Bootstrap.prefab`.

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

`ResourcesAssetProvider` serializes same-key requests. One caller performs the load; later same-key callers reuse the cached result instead of issuing duplicate Resources loads.

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
