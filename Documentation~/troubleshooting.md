# Troubleshooting

## Package compiles but project is not configured
Run `Tools > HP Framework > Setup` and choose **Setup / Repair Missing References**.

Normal Repair does not overwrite existing UICamera, Canvas Scaler, layout, manager ownership, catalogs, Input Actions or Toast assignments. If you intentionally want the stock framework values and the canonical domain-owned Bootstrap hierarchy (`Audio`, `UI`, `Input`, `Scene`, `Pools`, `Haptics`), use **Reset Bootstrap To Framework Defaults**.

## Input System is not installed
In editable `Assets/Plugins` development mode the `HP.Framework.Input` assembly is optional and is excluded when `com.unity.inputsystem` is absent. Install the Input System package when the game needs `InputManager`; the rest of the framework does not need to fail compilation just because that optional module is missing. UGUI remains required by the framework UI stack.

## UI camera is not visible through the gameplay camera
Make sure the gameplay camera is tagged `MainCamera`. When URP is present, HP Framework sets the gameplay camera to Base, the framework UI camera to Overlay, and adds it to the camera stack. If the current renderer does not expose a usable camera stack yet, the helper fails safely and retries during later scene initialization.

## Bootstrap/settings are missing
The setup tool recreates the framework Bootstrap/settings under `Assets/Plugins/HPFramework/Generated` and `Assets/Plugins/HPFramework/Settings`.

## Duplicate VContainer or UniTask assemblies
HP Framework currently bundles both dependencies under `ThirdParty`. Remove other source/package copies from the consuming project or adopt one dependency strategy consistently.

## Test assemblies are unavailable
Package tests are framework-development contracts. They are not required for normal game use. Enable package tests only when validating the framework itself.

## Save/load path errors
`JsonSaveFile` validates resolved paths so a relative folder cannot escape `Application.persistentDataPath`. Concurrent operations targeting the same file are serialized to avoid `.tmp`/`.bak` races.

## Duplicate Resources loads
`ResourcesAssetProvider` deduplicates concurrent requests for the same key. Callers awaiting the same key share the in-flight operation instead of triggering duplicate loads.

## Safe Area
The Safe Area utility lives under `ThirdParty/SafeArea`. It polls screen safe-area changes at a reduced interval rather than every frame. If it is attached to an object without a `RectTransform`, it disables itself and reports the configuration problem instead of destroying the GameObject. The demo is under `Samples~/04 Safe Area`.

## Repository validation
Run `.github/scripts/Validate-Package.ps1` from the framework repository.
