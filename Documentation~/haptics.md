# Haptics

Use semantic feedback when game intent is known:

```csharp
haptics.Play(HapticType.Selection);
haptics.Play(HapticType.Success);
haptics.Play(HapticType.HeavyImpact);
```

`VibrateShort`, `VibrateLong`, and `VibrateCustom` remain available for compatibility and raw-duration use cases.

`HapticManager` owns an `IHapticBackend`; platform calls do not live in the manager. Android caches its vibrator and API-level helpers, uses `VibrationEffect` on API 26+, and falls back to the legacy duration call on older versions. iOS currently uses `Handheld.Vibrate` as a documented fallback until an optional native semantic module is supplied. Unsupported devices fail silently without breaking gameplay.

The persisted vibration preference gates both semantic and raw calls. Backends are disposed with the manager and can be replaced with a fake through `ConfigureBackend` for automated tests.

Android players still require the vibration permission in the final manifest:

```xml
<uses-permission android:name="android.permission.VIBRATE" />
```
