# Migration to HP Framework 3.0

3.0 is a breaking package/distribution migration.

- Package name: `com.base.vcontainer` -> `com.hp.framework`
- Package root: old `_Base/` layout -> repository root UPM layout
- Assemblies: `_Base` / `Base.*` -> `HP.Framework.*`
- Namespaces: `Base.*` and legacy global framework types -> `HP.Framework.*`
- During active development, Bootstrap/default settings live with the source under `Assets/Plugins/HPFramework`
- Setup menu moved to `Tools > HP Framework > Setup`
- The canonical Bootstrap is now domain-owned (`Audio`, `UI`, `Input`, `Scene`, `Pools`, `Haptics`) with UI mount points renamed to `ScreenRoot`, `PopupRoot`, `NotificationRoot` and `InputBlocker`
- Normal Repair preserves existing ownership/layout; **Reset Bootstrap To Framework Defaults** performs the intentional migration to the canonical hierarchy
- Graphics, Diagnostics and generic Extensions are retained as explicit HP.Framework modules; WaypointPathfinding and the IAP implementation remain excluded
- Json.NET 13.0.4 is bundled as the private assembly `HP.Framework.NewtonsoftJson` for `HP.Framework.Extensions`; no external Newtonsoft package is required

Because namespaces changed, application code must update its `using` directives and asmdef references. Serialized framework assets keep their existing Unity GUIDs where they were migrated into the 3.0 package.
