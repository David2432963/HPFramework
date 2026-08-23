# Mobile Performance Policy

`HP.Framework.Performance` owns runtime quality and frame pacing for HP Framework projects.

## Ownership

`PerformanceService` is the only framework runtime actuator allowed to write:

- `Application.targetFrameRate`
- `QualitySettings.SetQualityLevel(...)`
- framework-owned base overrides such as `QualitySettings.lodBias`

`SettingsManager` persists preferences only. Its legacy `TargetFrameRate` and `QualityLevel` properties remain compatibility preference fields in v3.x and do not apply runtime state.

Diagnostics may request a temporary FPS override through `IPerformanceDiagnosticsControl`; diagnostics must not write Unity frame-rate state directly.

## Runtime apply order

`PerformanceService.ApplyTier()` is deterministic:

1. resolve the requested profile, falling back to the catalog default/first usable profile;
2. apply Unity Quality level;
3. apply framework-owned explicit overrides such as target FPS and LOD bias;
4. run `IPerformanceRendererApplier` integrations in ascending `Order`;
5. run game `IPerformanceTierApplier` consumers in ascending `Order`;
6. commit `CurrentProfile`, `CurrentTier`, target FPS and applied state;
7. publish `TierChanged` after the committed state is observable.

Applying the same effective profile again is idempotent. `Reapply()` explicitly reapplies the current profile.

## Profiles and project setup

Setup/Repair creates deterministic defaults under `Assets/Plugins/HPFramework/Settings/Performance/`:

- Low profile: conservative quality, 30 FPS target;
- Medium profile: balanced quality, 60 FPS target;
- High profile: highest available Unity Quality level, 60 FPS target;
- `PerformanceCatalogSO` containing the default profiles;
- `DevicePerformancePolicySO` used by the default classifier.

The reusable Bootstrap template remains project-agnostic. Setup/Repair links the project-owned catalog and device policy to Generated Bootstrap. Project validation fails if either reference is missing.

## User preference

`IPerformancePreferenceService` separates automatic recommendation from manual selection:

- Auto uses `IDevicePerformanceClassifier.RecommendTier()`;
- a manual tier persists until the user explicitly returns to Auto;
- returning to Auto does not erase the user's last manual tier.

The default classifier is a configurable conservative recommendation based on available SystemInfo signals. Missing/unreliable signals contribute no score and fall back safely. It is not a benchmark or hardware authority.

## Frame-rate policy

`FrameRatePolicy` normalizes requested FPS to a display-friendly divisor when a meaningful refresh rate is available. Examples include 30/60 on 60 Hz, 30/45/90 on 90 Hz, and 30/40/60/120 on 120 Hz. Unlimited FPS is only returned when a profile explicitly allows it.

## Optional renderer and game integration

The base module has no URP dependency. Renderer-specific packages may register `IPerformanceRendererApplier`. Games may register `IPerformanceTierApplier` for particles, crowd density, NPC budgets, post processing, or other tier-specific policy. These appliers run only when a profile is applied, never every frame.
