using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace HP.Framework.Startup
{
    public enum StartupTaskCriticality
    {
        Required = 0,
        Optional = 1
    }

    public interface IStartupTask
    {
        string Id { get; }
        StartupTaskCriticality Criticality { get; }

        UniTask ExecuteAsync(
            IProgress<float> progress,
            CancellationToken cancellationToken);
    }

    public interface IProgressSource
    {
        float Progress { get; }
        event Action<float> ProgressChanged;
    }

    public interface IStartupCoordinator : IProgressSource
    {
        bool IsReady { get; }
        bool IsRunning { get; }
        IReadOnlyList<StartupFailure> Failures { get; }

        event Action Ready;
        event Action<StartupFailure> TaskFailed;
        event Action<StartupFailure> Failed;

        UniTask RunAsync(CancellationToken token = default);
    }
}
