# Audio

`AudioManager` owns one music source and a bounded pool of SFX voices. Configure the initial and maximum channel counts on the Bootstrap Audio object. The maximum is a hard ceiling: playback never escapes the budget through an untracked `PlayOneShot` source.

## Library entries

Each `AudioLibrarySO.AudioEntry` supports:

- `DirectClip` for small, frequently used UI/gameplay clips.
- `AssetKey` for music, long voice clips, ambient tracks, or content loaded for a limited event/scene.
- priority, per-key simultaneous limit, minimum retrigger interval, default spatial blend, and category metadata.

The defaults preserve the original direct-clip behavior: priority `0`, unlimited per-key voices, no retrigger delay, 2D playback, and `Gameplay` category.

## Voice selection

For every accepted SFX request, the service uses this deterministic order:

1. Reuse a free pooled channel.
2. Grow the pool only while below `maxSfxChannelCount`.
3. Steal the lowest-priority eligible voice; ties select the oldest voice.
4. Drop the request when all active voices have higher priority.

Use `IAudioDiagnostics.Stats` for active/max voices and cumulative played, dropped, and stolen counters. Reading the snapshot also retires completed voices; no per-frame scanner is installed.

## Asset ownership

Use `PlayMusicAsync` or `PlaySfxAsync` for `AssetKey` entries. The active music track or SFX voice owns its `IAssetLease<AudioClip>` and releases it on stop, replacement, steal, natural completion, or manager disposal. Asset-backed SFX use a lightweight per-voice completion watcher so the final voice can release its lease without waiting for another audio request or diagnostics read. Direct clips do not create leases.

The existing synchronous key APIs remain compatible with direct entries. When called for an asset-key entry they dispatch the asynchronous load; callers that need cancellation or readiness guarantees should call the async API explicitly.

## Spatial reuse

Every pooled channel resets its local position and spatial blend before playback. Positional APIs force fully spatial playback; non-positional key playback uses the entry's configured default blend.
