# Changelog

All notable changes to HP Framework are documented here.

## 3.0.0 - 2026-08-08

### Changed
- Rebuilt the repository as a true UPM package whose repository root is the package root.
- Renamed package identity to `com.hp.framework` / **HP Framework**.
- Renamed framework assemblies to `HP.Framework.*` and normalized framework namespaces.
- Moved runtime, editor and tests into standard UPM folders.
- Development-mode Bootstrap/settings now live with the editable source under `Assets/Plugins/HPFramework`.
- Added `Tools > HP Framework > Setup` with setup/repair and validation actions.
- Scoped UI and pooling remain first-class scene/feature infrastructure.
- Hardened URP UI camera setup: UICamera is configured as Overlay and attached exactly once to the active Main Camera, which is configured as Base when URP is available.
- Added scope-owned `IEventBus` / `EventBus` with `RegisterScopeEventBus()` while retaining the static `Observer` compatibility API.
- Setup repair is now non-destructive by default; framework UI defaults are reapplied only through the explicit reset action.
- Reorganized the default Bootstrap into domain-owned `Audio`, `UI`, `Input`, `Scene`, `Pools`, and `Haptics` roots; renamed UI mount points to `ScreenRoot`, `PopupRoot`, `NotificationRoot`, and `InputBlocker`; and made Reset migrate the legacy layout while normal Repair preserves existing ownership.
- Made the Input System integration optional in editable `Assets/Plugins` mode by removing Bootstrap/Editor hard references and gating `HP.Framework.Input` with a package version define.
- Optimized pooling by caching `IPoolable` components per instance instead of scanning hierarchies on every spawn/despawn.
- Hardened UI catalog replacement/clear paths so registered views are destroyed instead of becoming orphaned.
- Serialized JSON file operations per save path and reject persistence path traversal outside `Application.persistentDataPath`.
- Reduced per-frame Graphics/Diagnostics/Safe Area work, added Resources same-key load deduplication, and added non-alloc physics query APIs.
- Moved Ripple and normal scaled-time Bubbling Surface animation to shader-driven GPU clocks so effect time is not pushed through a MaterialPropertyBlock every frame; unscaled/edit-mode Bubbling playback retains a CPU fallback.
- Added caller-owned/non-alloc frustum-plane APIs to `CameraUtils` for bulk visibility checks while keeping the existing convenience overloads.

### Restored / retained
- Restored Graphics as `HP.Framework.Graphics`.
- Restored Diagnostics as `HP.Framework.Diagnostics`.
- Restored generic utility extensions as `HP.Framework.Extensions` with a bundled private Json.NET 13.0.4 assembly (`HP.Framework.NewtonsoftJson`).
- Restored the Safe Area utility under `ThirdParty/SafeArea` as `HP.Framework.SafeArea`; its demo content now lives under `Samples~/04 Safe Area` instead of the production runtime tree.

### Removed
- Removed legacy Sirenix/Odin, WaypointPathfinding and the old IAP implementation from the framework.
- Removed old `_Base` package layout and project-specific paths.

### Distribution
- VContainer and UniTask remain bundled to keep Git/local UPM installation self-contained.
- Added GitHub repository validation, package-grade ignore/attributes files, Samples and Documentation folders.

### Breaking
- 3.0.0 is a structural/API namespace migration from the previous Base package. See `Documentation~/migration-3.0.md`.
