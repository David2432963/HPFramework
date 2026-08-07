# Changelog

## 1.1.0 - 2026-08-07

### Added

- Static package integrity validator.
- Explicit `IProcedureService` contract and transition states.
- Cancellation-aware `UniTask` scene loading API.
- `ResourcesAssetProvider` as dependency-free default provider.
- Mutable `ISettingsService` as shared settings source.
- `IPoolable` spawn/despawn lifecycle.
- Separate runtime, editor, diagnostics, graphics, waypoint and test assemblies.
- EditMode procedure/utility/prefab tests.
- PlayMode Bootstrap container smoke test.

### Changed

- Procedure registration is explicit through VContainer; assembly scanning and `Activator` fallback were removed.
- VContainer EntryPoints are now the only framework service lifecycle.
- Bootstrap prefab now references real Unity script GUIDs and contains no Missing Script.
- UI/audio transitions use Unity coroutine and `AnimationCurve` implementations instead of hard DOTween references.
- URP camera stacking is optional and detected at runtime.
- Settings, audio and haptics share one preference source.
- Pooling now protects against duplicate release and limits inactive capacity.
- JSON saves use atomic writes and keep Unity serialization on the main thread.
- Input enables one action map context at a time.
- Simulated IAP is limited to Editor and Development builds.

### Removed

- `MonoManager`, legacy `Main.Instance` dispatch and reflection-based MonoBehaviour scanning.
- Core hard references to Addressables, DOTween, Odin Inspector, URP, Entities and Unity Collections.
- Automatic `Resources.UnloadUnusedAssets()` calls during normal scene/asset release flow.
- Embedded UniTask Addressables/DOTween adapters and unused Odin Unity.Mathematics integration.

### Validation

- Unity package import and script compilation passed on Unity `6000.0.77f1`.
- EditMode tests: `7/7` passed.
- PlayMode tests: `1/1` passed.
