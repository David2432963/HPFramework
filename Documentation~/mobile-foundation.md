# Mobile foundation

HP Framework 3.0 provides explicit ownership and bounded runtime behavior for the infrastructure shared by mobile games.

## Runtime ownership map

- `IStartupCoordinator` is the async readiness boundary. Scene entry points await it before starting gameplay ticks.
- `IApplicationLifecycle` publishes pause, focus, low-memory, and quit signals. Bootstrap adapters flush settings and trim inactive pools/assets.
- `IPerformanceService` owns target FPS and Unity quality actuation; player preferences do not mutate Unity state directly.
- `IAssetLeaseProvider` deduplicates shared loads and retains content only while explicit leases exist.
- `IPoolService` supports batched prewarm, inactive capacity, low-memory trim, and immutable diagnostics.
- `IAudioService` enforces a hard voice budget and retains asset-backed clips only while active.
- `InputManager` keeps primary switch semantics separate from ref-counted overlay map leases.
- `IHapticService` maps semantic intent to platform backends.
- `IUIService` keeps direct prefabs synchronous and provides shared/cancellation-safe async creation for asset-key entries.
- The opt-in diagnostics HUD samples these service snapshots at a throttled interval.

## Setup and validation

Run `Tools > HP Framework > Setup` and choose **Setup / Repair Missing References**. Repair fills missing ownership points and references but preserves custom catalogs, Input Actions, camera/canvas values, manager hierarchy, and performance configuration. **Reset Bootstrap To Framework Defaults** is intentionally destructive and requires confirmation.

Run **Validate Project** after setup and before release candidates. It checks the preloaded root, required and duplicate managers, Input Action ownership, performance references, pool budgets, and Audio/UI catalog entries. Runtime startup construction separately rejects empty or duplicate startup task IDs.

## Release-candidate checklist

1. Run focused tests for the modules changed by the game.
2. Run full `HP.Framework.Tests.Editor` and `HP.Framework.Tests.Runtime` suites.
3. Run `.github/scripts/Validate-Package.ps1` from the framework root.
4. Build Android with IL2CPP, ARM64, managed stripping, and a Development build for diagnostics smoke.
5. Launch on a low/mid target device; verify boot readiness, background/resume, low-memory handling, input-map transitions, async UI cancellation, audio hard cap, haptics preference, and diagnostics availability fallbacks.
6. Build a non-Development release candidate and confirm the diagnostics overlay remains disabled unless explicitly opted in.

Editor memory and frame timing are not device acceptance evidence. Record device model, OS, graphics API, quality tier, target FPS, peak managed memory, thermal state, and any unavailable counters with each smoke result.
