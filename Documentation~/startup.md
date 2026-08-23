# Async startup and readiness

`HP.Framework.Startup` defines the asynchronous application-ready boundary. It does not replace VContainer lifecycle hooks.

## Boundary

Use `IInitializable` for synchronous construction and wiring: subscribing callbacks, building small lookups, and loading tiny local preference caches. Use `IStartupTask` only for work that genuinely needs awaiting, timeout, retry, cancellation, or progress such as content catalogs, profile loads, consent, remote config, IAP/auth, or game boot readiness.

The Startup assembly depends only on Core and UniTask. Persistence, Assets, UI, platform SDKs, and game code provide tasks from higher layers through Bootstrap composition.

## Registering tasks

Derive from `RootLifetimeScope` and register application tasks in `RegisterApplicationStartupTasks`:

```csharp
protected override void RegisterApplicationStartupTasks(IContainerBuilder builder)
{
    builder.RegisterStartupTask<LoadProfileTask>(
        stage: 1,
        timeout: TimeSpan.FromSeconds(10),
        retryCount: 2,
        retryDelay: TimeSpan.FromMilliseconds(250));
}
```

The extension owns the task's singleton registration and creates coordinator-owned metadata. `IStartupTask.Criticality` determines required vs optional behavior.

## Execution model

Stages execute in ascending numeric order. Tasks within one stage start in parallel on the Unity main-thread orchestration context; the coordinator does not silently move tasks to a worker thread. A task may explicitly switch to a worker thread for pure CPU/disk/network processing, but UnityEngine object access must return to the main thread when required.

Progress is aggregated equally across registered tasks and is monotonic in the range 0..1. Task progress values that move backward are ignored.

Required final failure stops later stages and raises `Failed`. Optional final failure is recorded in `Failures`, raises `TaskFailed`, and startup continues. Retry count means retries after the initial attempt, so `retryCount: 2` allows exactly three attempts.

Timeout is coordinator-owned. On timeout the task cancellation token is cancelled and a contextual `StartupFailure` is produced. Startup tasks are expected to honor cancellation promptly; code that ignores cancellation can continue side effects after its logical timeout and is therefore invalid startup-task behavior.

Concurrent `RunAsync` calls share the in-progress execution instead of executing tasks twice. After readiness, subsequent calls return immediately and `Ready` is published only once.

Bootstrap registers the coordinator but does **not** auto-run it from `Awake`, `IInitializable`, or `IStartable`. The application boot flow explicitly calls `RunAsync()` at the point where asynchronous readiness should begin. This keeps `container built` and `application ready` as separate states.

## Loading UI

Startup progress and scene-loading progress are separate domains. Presenters may consume `IProgressSource`, but StartupCoordinator is not merged into GameSceneManager.
