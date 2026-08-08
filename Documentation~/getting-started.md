# Getting started

1. Place or clone HP Framework at `Assets/Plugins/HPFramework`.
2. Open `Tools > HP Framework > Setup`.
3. Click **Setup / Repair Missing References**.
4. Click **Validate Project**.
5. Edit framework source directly under `Assets/Plugins/HPFramework` while it is still under development.
6. Keep game-specific code in your own `Assets/Game` (or equivalent) folders.

Setup creates or repairs the local Bootstrap and default settings under `Assets/Plugins/HPFramework/Generated` and `Assets/Plugins/HPFramework/Settings`. These are host-project outputs and are intentionally ignored by the framework Git repository; reusable source/templates stay tracked.

A game root can derive from `RootLifetimeScope` and override registration hooks for application-owned services and entry points. Scene gameplay should use `BaseSceneLifetimeScope`; shorter-lived features should use `BaseFeatureLifetimeScope`.

## Startup order

`RootLifetimeScope` inherits VContainer's early execution order (`-5000`). With `autoRun` enabled, its container is built from `Awake()`, and synchronous `IInitializable` services are dispatched during that build. This means framework services initialize before ordinary MonoBehaviours that use Unity's default execution order.

Do not treat raw `Awake()` ordering as an async application-ready barrier. If the application later adds remote config, authentication, Addressables catalog loading, IAP initialization, or similar async startup work, gate dependent application flow through VContainer lifecycle/readiness services rather than relying only on script execution order.

## UI camera in URP

When URP is available, HP Framework configures the framework `UICamera` as an Overlay camera, configures `Camera.main` as Base, and attaches the UI camera to the main camera stack without adding duplicates. The attachment is retried on scene load so replacing the gameplay camera between Loading/Menu/Gameplay scenes is supported.

The gameplay camera must use the `MainCamera` tag so Unity can resolve it through `Camera.main`.
