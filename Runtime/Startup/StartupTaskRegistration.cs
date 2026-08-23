using System;

namespace HP.Framework.Startup
{
    public readonly struct StartupTaskOptions
    {
        public StartupTaskOptions(
            int stage,
            TimeSpan timeout,
            int retryCount = 0,
            TimeSpan retryDelay = default)
        {
            if (stage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stage));
            }

            if (timeout < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            if (retryCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(retryCount));
            }

            if (retryDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(retryDelay));
            }

            Stage = stage;
            Timeout = timeout;
            RetryCount = retryCount;
            RetryDelay = retryDelay;
        }

        public int Stage { get; }
        public TimeSpan Timeout { get; }
        public int RetryCount { get; }
        public TimeSpan RetryDelay { get; }

        public static StartupTaskOptions Default(int stage)
            => new StartupTaskOptions(stage, TimeSpan.FromSeconds(30), 0, TimeSpan.Zero);
    }

    public interface IStartupTaskRegistration
    {
        IStartupTask Task { get; }
        StartupTaskOptions Options { get; }
    }

    public sealed class StartupTaskRegistration : IStartupTaskRegistration
    {
        public StartupTaskRegistration(IStartupTask task, StartupTaskOptions options)
        {
            Task = task ?? throw new ArgumentNullException(nameof(task));
            Options = options;
        }

        public IStartupTask Task { get; }
        public StartupTaskOptions Options { get; }
    }

    public sealed class StartupTaskRegistration<TTask> : IStartupTaskRegistration
        where TTask : class, IStartupTask
    {
        public StartupTaskRegistration(TTask task, StartupTaskOptions options)
        {
            Task = task ?? throw new ArgumentNullException(nameof(task));
            Options = options;
        }

        public IStartupTask Task { get; }
        public StartupTaskOptions Options { get; }
    }
}
