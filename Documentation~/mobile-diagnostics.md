# Mobile diagnostics

`Runtime/Diagnostics/Prefabs/RuntimeDebugOverlay.prefab` provides an opt-in on-device HUD. Its `FpsDisplay` now renders a unified snapshot instead of updating independent metrics every frame.

The snapshot includes average FPS, frame time, target FPS, performance tier, managed memory, GC allocation when `ProfilerRecorder` is available, CPU/GPU frame timing when `FrameTimingManager` reports data, plus pool, asset, audio, and input ownership statistics.

Unavailable platform or framework metrics render as `n/a`; unsupported profiler counters never prevent the HUD from running.

## Sampling and builds

- Sampling is throttled by `refreshInterval` (default prefab value `0.25s`).
- UI strings are rebuilt only when a new snapshot is emitted.
- Profiler/frame-timing resources are disposed when the overlay is disabled or destroyed.
- The overlay runs in the Editor and Development Builds.
- Non-development players disable it unless `includeInReleaseBuild` is explicitly enabled.

Keep the overlay out of normal production UI flow. Add the prefab only to a debug/developer composition path and inject it through the root VContainer scope so all diagnostics interfaces resolve.

## Extending metrics

Framework modules expose immutable lightweight snapshots through `IAssetDiagnostics`, `IPoolDiagnostics`, `IAudioDiagnostics`, and `IInputDiagnostics`. Add metrics to those contracts rather than reading manager private fields or enumerating large collections from the HUD.
