using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using HP.Framework.Startup;
using NUnit.Framework;

namespace HP.Framework.Tests.Editor
{
    public sealed class StartupCoordinatorTests
    {
        private sealed class FakeTask : IStartupTask
        {
            private readonly Func<IProgress<float>, CancellationToken, UniTask> execute;

            public FakeTask(
                string id,
                StartupTaskCriticality criticality,
                Func<IProgress<float>, CancellationToken, UniTask> execute)
            {
                Id = id;
                Criticality = criticality;
                this.execute = execute;
            }

            public string Id { get; }
            public StartupTaskCriticality Criticality { get; }
            public int Attempts { get; private set; }

            public UniTask ExecuteAsync(IProgress<float> progress, CancellationToken cancellationToken)
            {
                Attempts++;
                return execute(progress, cancellationToken);
            }
        }

        [Test]
        public async Task OrderedStages_AndParallelTasksWithinStage()
        {
            var events = new List<string>();
            var gate = new UniTaskCompletionSource();
            var first = new FakeTask("first", StartupTaskCriticality.Required, async (_, token) =>
            {
                events.Add("first-start");
                await gate.Task.AttachExternalCancellation(token);
                events.Add("first-end");
            });
            var parallel = new FakeTask("parallel", StartupTaskCriticality.Required, async (_, token) =>
            {
                events.Add("parallel-start");
                await gate.Task.AttachExternalCancellation(token);
                events.Add("parallel-end");
            });
            var later = new FakeTask("later", StartupTaskCriticality.Required, (_, __) =>
            {
                events.Add("later");
                return UniTask.CompletedTask;
            });

            var coordinator = CreateCoordinator(
                Register(first, 0),
                Register(parallel, 0),
                Register(later, 1));
            UniTask run = coordinator.RunAsync();
            await UniTask.Yield();

            Assert.That(events, Does.Contain("first-start"));
            Assert.That(events, Does.Contain("parallel-start"));
            Assert.That(events, Does.Not.Contain("later"));

            gate.TrySetResult();
            await run;
            Assert.That(events.IndexOf("later"), Is.GreaterThan(events.IndexOf("first-end")));
            Assert.That(events.IndexOf("later"), Is.GreaterThan(events.IndexOf("parallel-end")));
        }

        [Test]
        public async Task Progress_IsMonotonic_AndEndsAtOne()
        {
            var task = new FakeTask("progress", StartupTaskCriticality.Required, (progress, _) =>
            {
                progress.Report(0.6f);
                progress.Report(0.2f);
                progress.Report(0.9f);
                return UniTask.CompletedTask;
            });
            var coordinator = CreateCoordinator(Register(task, 0));
            var observed = new List<float>();
            coordinator.ProgressChanged += observed.Add;

            await coordinator.RunAsync();

            Assert.That(coordinator.Progress, Is.EqualTo(1f));
            Assert.That(observed, Is.Not.Empty);
            for (int i = 1; i < observed.Count; i++)
            {
                Assert.That(observed[i], Is.GreaterThanOrEqualTo(observed[i - 1]));
            }
        }

        [Test]
        public void RequiredFailure_StopsLaterStages_WithTaskContext()
        {
            var required = new FakeTask("required-failure", StartupTaskCriticality.Required,
                (_, __) => UniTask.FromException(new InvalidOperationException("boom")));
            var later = new FakeTask("later", StartupTaskCriticality.Required,
                (_, __) => UniTask.CompletedTask);
            var coordinator = CreateCoordinator(Register(required, 0), Register(later, 1));

            StartupFailedException exception = Assert.ThrowsAsync<StartupFailedException>(
                async () => await coordinator.RunAsync());

            Assert.That(exception.Message, Does.Contain("required-failure"));
            Assert.That(exception.Failure.TaskId, Is.EqualTo("required-failure"));
            Assert.That(later.Attempts, Is.Zero);
            Assert.That(coordinator.IsReady, Is.False);
        }

        [Test]
        public async Task OptionalFailure_IsRecorded_AndStartupContinues()
        {
            var optional = new FakeTask("optional", StartupTaskCriticality.Optional,
                (_, __) => UniTask.FromException(new InvalidOperationException("optional failure")));
            var later = new FakeTask("later", StartupTaskCriticality.Required,
                (_, __) => UniTask.CompletedTask);
            var coordinator = CreateCoordinator(Register(optional, 0), Register(later, 1));

            await coordinator.RunAsync();

            Assert.That(coordinator.IsReady, Is.True);
            Assert.That(coordinator.Failures, Has.Count.EqualTo(1));
            Assert.That(coordinator.Failures[0].TaskId, Is.EqualTo("optional"));
            Assert.That(later.Attempts, Is.EqualTo(1));
        }

        [Test]
        public void Retry_UsesExactConfiguredAttemptCount()
        {
            var task = new FakeTask("retry", StartupTaskCriticality.Required, (_, __) =>
                UniTask.FromException(new InvalidOperationException("retry")));
            var coordinator = CreateCoordinator(new StartupTaskRegistration(
                task,
                new StartupTaskOptions(0, TimeSpan.Zero, retryCount: 2)));

            Assert.ThrowsAsync<StartupFailedException>(async () => await coordinator.RunAsync());
            Assert.That(task.Attempts, Is.EqualTo(3));
            Assert.That(coordinator.Failures.Single().Attempts, Is.EqualTo(3));
        }

        [Test]
        public async Task Timeout_IsReportedWithTaskId()
        {
            var task = new FakeTask("timeout-task", StartupTaskCriticality.Required,
                (_, token) => UniTask.Never(token));
            var coordinator = CreateCoordinator(new StartupTaskRegistration(
                task,
                new StartupTaskOptions(0, TimeSpan.FromMilliseconds(20))));

            StartupFailedException exception = null;
            try
            {
                await coordinator.RunAsync();
            }
            catch (StartupFailedException caught)
            {
                exception = caught;
            }

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.Failure.TimedOut, Is.True);
            Assert.That(exception.Failure.TaskId, Is.EqualTo("timeout-task"));
        }

        [Test]
        public async Task Cancellation_PropagatesWithoutPublishingRequiredFailure()
        {
            var task = new FakeTask("cancel", StartupTaskCriticality.Required,
                (_, token) => UniTask.Never(token));
            var coordinator = CreateCoordinator(Register(task, 0));
            using var cancellation = new CancellationTokenSource();
            cancellation.CancelAfter(20);

            OperationCanceledException exception = null;
            try
            {
                await coordinator.RunAsync(cancellation.Token);
            }
            catch (OperationCanceledException caught)
            {
                exception = caught;
            }

            Assert.That(exception, Is.Not.Null);
            Assert.That(coordinator.Failures, Is.Empty);
            Assert.That(coordinator.IsReady, Is.False);
        }

        [Test]
        public async Task DoubleRun_SharesExecution_AndReadyFiresOnce()
        {
            var gate = new UniTaskCompletionSource();
            var task = new FakeTask("shared", StartupTaskCriticality.Required,
                (_, token) => gate.Task.AttachExternalCancellation(token));
            var coordinator = CreateCoordinator(Register(task, 0));
            int readyCount = 0;
            coordinator.Ready += () => readyCount++;

            UniTask first = coordinator.RunAsync();
            UniTask second = coordinator.RunAsync();
            await UniTask.Yield();
            Assert.That(task.Attempts, Is.EqualTo(1));

            gate.TrySetResult();
            await first;
            await second;
            await coordinator.RunAsync();

            Assert.That(task.Attempts, Is.EqualTo(1));
            Assert.That(readyCount, Is.EqualTo(1));
            Assert.That(coordinator.IsReady, Is.True);
        }

        [Test]
        public void DuplicateIds_AreRejectedDeterministically()
        {
            var first = new FakeTask("duplicate", StartupTaskCriticality.Required,
                (_, __) => UniTask.CompletedTask);
            var second = new FakeTask("duplicate", StartupTaskCriticality.Optional,
                (_, __) => UniTask.CompletedTask);

            Assert.Throws<ArgumentException>(() => CreateCoordinator(
                Register(first, 0),
                Register(second, 1)));
        }

        private static StartupCoordinator CreateCoordinator(params IStartupTaskRegistration[] registrations)
            => new StartupCoordinator(registrations);

        private static StartupTaskRegistration Register(IStartupTask task, int stage)
            => new StartupTaskRegistration(task, new StartupTaskOptions(stage, TimeSpan.Zero));
    }
}
