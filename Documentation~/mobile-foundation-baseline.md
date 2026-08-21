# Mobile foundation baseline

This report is the M0.5 measurement/build checkpoint captured before the mobile-foundation roadmap changes beyond Input stabilization.

## Environment

- Host project: Airport Security Simulation
- Unity: 6000.0.77f1
- Baseline date: 2026-08-21
- Runtime measurement host: WindowsEditor PlayMode, isolated validation copy
- Android baseline: Development build, IL2CPP, ARM64 only, Managed Stripping Level = Medium

## Runtime baseline

The isolated PlayMode harness used 60 warm-up frames and 180 sample frames while instantiating the current framework Bootstrap.

| Metric | Baseline |
| --- | ---: |
| Bootstrap instantiate | 51.3866 ms |
| Average sampled frame | 16.6668 ms |
| Maximum sampled frame | 18.0211 ms |
| Mono used memory | 830,902,272 bytes |
| Mono heap | 1,016,156,160 bytes |
| Root pool idle child count | 0 |
| Asset-provider loaded count | unavailable in pre-M2 public API |
| Editor-process GC allocation recorder | 410,432.16 bytes/frame |

The GC recorder value is deliberately **not** labeled framework-owned GC. Unity Editor, PlayMode Test Runner and other editor systems share that recorder, so the value is only an editor-process upper-bound reference. A later diagnostics milestone must provide attributable framework counters before any framework-only GC claim is made.

## Android IL2CPP baseline

The Android package completed successfully with the required baseline configuration.

- Result: Success
- Output: `Builds/M0_5_AndroidBaseline.apk` in the isolated validation project
- APK size: 50,322,264 bytes
- Player build duration reported by Unity: 520,878 ms
- Backend: IL2CPP
- Architecture: ARM64
- Development Build: enabled
- Managed Stripping Level: Medium

No iOS Xcode generation was run because this validation host is Windows. No real-device Android smoke result is claimed for this checkpoint because a device baseline was not established in this session.

## Linker / stripping risk audit

The current Android IL2CPP + Medium stripping build passing is the primary smoke test for the current registered framework graph. Known reflection-sensitive areas remain:

- VContainer runtime injector discovery and open-generic/unmanaged-system paths use reflection/`Activator`. VContainer includes preserve annotations/generated-injector support; future reflection-only registrations still need IL2CPP smoke coverage.
- `URPCameraStackUtility` resolves `UniversalAdditionalCameraData` dynamically. URP-specific player support must keep an integration smoke test when enabled.
- `DotweenBridge` discovers/invokes optional DOTween APIs through reflection. If DOTween is enabled in a player build, preservation/linker requirements must be validated in that optional integration slice.
- `ObjectArrayExtensions` can instantiate collection target types through `Activator.CreateInstance`; types reachable only through dynamic conversion may require preservation.
- Editor-only reflection in Setup/Inspector tooling is not part of player stripping risk.

There is no base-package `link.xml` requirement identified for the current M0.5 graph. Add `[Preserve]`/`link.xml` only for concrete reflection-only player types that fail or are proven at risk; do not preserve whole assemblies speculatively.

## Baseline rule

Future milestones compare against this checkpoint where the metric is actually attributable. No performance improvement should be claimed solely from Editor compile success or from this non-attributed GC recorder value.
