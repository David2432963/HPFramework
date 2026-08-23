using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace HP.Framework.Startup
{
    /// <summary>
    /// Executes asynchronous application-readiness work in ordered stages. Tasks in the same stage
    /// begin together and are awaited together. The coordinator never calls Task.Run or otherwise
    /// moves tasks off the Unity main thread implicitly.
    /// </summary>
    public sealed class StartupCoordinator : IStartupCoordinator
    {

        private sealed class ProgressReporter : IProgress<float>
        {
            private readonly Action<float> report;

            public ProgressReporter(Action<float> report)
            {
                this.report = report;
            }

            public void Report(float value)
            {
                report?.Invoke(value);
            }
        }

        private readonly object syncRoot = new object();
        private readonly List<IStartupTaskRegistration> registrations;
        private readonly List<StartupFailure> failures = new List<StartupFailure>();
        private readonly float[] taskProgress;

        private UniTaskCompletionSource runCompletion;
        private bool isRunning;
        private bool isReady;
        private float progress;

        public StartupCoordinator(IEnumerable<IStartupTaskRegistration> registrations)
        {
            this.registrations = registrations != null
                ? registrations.Where(item => item != null).ToList()
                : new List<IStartupTaskRegistration>();

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < this.registrations.Count; i++)
            {
                IStartupTask task = this.registrations[i].Task;
                if (task == null)
                {
                    throw new ArgumentException("Startup registration contains a null task.", nameof(registrations));
                }

                if (string.IsNullOrWhiteSpace(task.Id))
                {
                    throw new ArgumentException(
                        $"Startup task '{task.GetType().FullName}' has an empty Id.",
                        nameof(registrations));
                }

                if (!ids.Add(task.Id))
                {
                    throw new ArgumentException(
                        $"Duplicate startup task Id '{task.Id}' is not allowed.",
                        nameof(registrations));
                }
            }

            this.registrations.Sort((left, right) =>
            {
                int stage = left.Options.Stage.CompareTo(right.Options.Stage);
                return stage != 0
                    ? stage
                    : string.CompareOrdinal(left.Task.Id, right.Task.Id);
            });
            taskProgress = new float[this.registrations.Count];
        }

        public bool IsRunning
        {
            get { lock (syncRoot) return isRunning; }
        }

        public bool IsReady
        {
            get { lock (syncRoot) return isReady; }
        }

        public float Progress
        {
            get { lock (syncRoot) return progress; }
        }

        public IReadOnlyList<StartupFailure> Failures => failures;

        public event Action<float> ProgressChanged;
        public event Action Ready;
        public event Action<StartupFailure> TaskFailed;
        public event Action<StartupFailure> Failed;

        public UniTask RunAsync(CancellationToken cancellationToken = default)
        {
            lock (syncRoot)
            {
                if (runCompletion != null)
                {
                    return runCompletion.Task;
                }

                runCompletion = new UniTaskCompletionSource();
                isRunning = true;
            }

            RunCoreAsync(cancellationToken).Forget();
            return runCompletion.Task;
        }

        private async UniTaskVoid RunCoreAsync(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (registrations.Count == 0)
                {
                    UpdateOverallProgress(1f);
                    CompleteReady();
                    runCompletion.TrySetResult();
                    return;
                }

                int cursor = 0;
                while (cursor < registrations.Count)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int stage = registrations[cursor].Options.Stage;
                    int stageEnd = cursor + 1;
                    while (stageEnd < registrations.Count
                           && registrations[stageEnd].Options.Stage == stage)
                    {
                        stageEnd++;
                    }

                    var stageTasks = new List<UniTask<StartupFailure>>(stageEnd - cursor);
                    for (int i = cursor; i < stageEnd; i++)
                    {
                        stageTasks.Add(RunExecutionAsync(registrations[i], i, cancellationToken));
                    }

                    StartupFailure[] results = await UniTask.WhenAll(stageTasks);
                    StartupFailure requiredFailure = null;
                    for (int i = 0; i < results.Length; i++)
                    {
                        StartupFailure failure = results[i];
                        if (failure == null)
                        {
                            continue;
                        }

                        failures.Add(failure);
                        TaskFailed?.Invoke(failure);
                        if (failure.Criticality == StartupTaskCriticality.Required && requiredFailure == null)
                        {
                            requiredFailure = failure;
                        }
                    }

                    if (requiredFailure != null)
                    {
                        lock (syncRoot)
                        {
                            isRunning = false;
                        }

                        Failed?.Invoke(requiredFailure);
                        runCompletion.TrySetException(new StartupFailedException(requiredFailure));
                        return;
                    }

                    cursor = stageEnd;
                }

                CompleteReady();
                runCompletion.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                lock (syncRoot)
                {
                    isRunning = false;
                }

                runCompletion.TrySetCanceled(cancellationToken);
            }
            catch (Exception exception)
            {
                lock (syncRoot)
                {
                    isRunning = false;
                }

                runCompletion.TrySetException(exception);
            }
        }

        private async UniTask<StartupFailure> RunExecutionAsync(
            IStartupTaskRegistration registration,
            int taskIndex,
            CancellationToken cancellationToken)
        {
            IStartupTask task = registration.Task;
            StartupTaskOptions options = registration.Options;
            int maxAttempts = options.RetryCount + 1;
            Exception lastException = null;
            bool timedOut = false;

            var reporter = new ProgressReporter(value => ReportTaskProgress(taskIndex, value));
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                timedOut = false;

                try
                {
                    using var attemptCancellation =
                        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    UniTask execution = task.ExecuteAsync(reporter, attemptCancellation.Token);
                    if (options.Timeout > TimeSpan.Zero)
                    {
                        await execution.Timeout(
                            options.Timeout,
                            taskCancellationTokenSource: attemptCancellation);
                    }
                    else
                    {
                        await execution;
                    }

                    ReportTaskProgress(taskIndex, 1f);
                    return null;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    lastException = exception;
                    timedOut = exception is TimeoutException;
                    if (attempt >= maxAttempts)
                    {
                        ReportTaskProgress(taskIndex, 1f);
                        return new StartupFailure(
                            task.Id,
                            task.Criticality,
                            options.Stage,
                            attempt,
                            timedOut,
                            exception);
                    }
                }

                if (options.RetryDelay > TimeSpan.Zero)
                {
                    await UniTask.Delay(
                        options.RetryDelay,
                        ignoreTimeScale: true,
                        cancellationToken: cancellationToken);
                }
            }

            throw new InvalidOperationException(
                $"Startup task '{task.Id}' exhausted retries without a terminal result.",
                lastException);
        }

        private void ReportTaskProgress(int index, float value)
        {
            float nextOverall;
            lock (syncRoot)
            {
                float clamped = Mathf.Clamp01(value);
                if (clamped <= taskProgress[index])
                {
                    return;
                }

                taskProgress[index] = clamped;
                float sum = 0f;
                for (int i = 0; i < taskProgress.Length; i++)
                {
                    sum += taskProgress[i];
                }

                nextOverall = taskProgress.Length > 0 ? sum / taskProgress.Length : 1f;
                if (nextOverall <= progress)
                {
                    return;
                }

                progress = nextOverall;
            }

            ProgressChanged?.Invoke(nextOverall);
        }

        private void UpdateOverallProgress(float value)
        {
            float clamped = Mathf.Clamp01(value);
            lock (syncRoot)
            {
                if (clamped <= progress)
                {
                    return;
                }

                progress = clamped;
            }

            ProgressChanged?.Invoke(clamped);
        }

        private void CompleteReady()
        {
            UpdateOverallProgress(1f);
            bool invokeReady;
            lock (syncRoot)
            {
                isRunning = false;
                invokeReady = !isReady;
                isReady = true;
            }

            if (invokeReady)
            {
                Ready?.Invoke();
            }
        }
    }
}
