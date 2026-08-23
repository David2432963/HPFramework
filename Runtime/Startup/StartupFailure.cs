using System;

namespace HP.Framework.Startup
{
    public sealed class StartupFailure
    {
        public StartupFailure(
            string taskId,
            StartupTaskCriticality criticality,
            int stage,
            int attempts,
            bool timedOut,
            Exception exception)
        {
            TaskId = string.IsNullOrWhiteSpace(taskId)
                ? throw new ArgumentException("Startup task ID must be non-empty.", nameof(taskId))
                : taskId;
            Criticality = criticality;
            Stage = stage;
            Attempts = attempts;
            TimedOut = timedOut;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public string TaskId { get; }
        public StartupTaskCriticality Criticality { get; }
        public int Stage { get; }
        public int Attempts { get; }
        public bool TimedOut { get; }
        public Exception Exception { get; }
    }

    public sealed class StartupFailedException : Exception
    {
        public StartupFailedException(StartupFailure failure)
            : base(
                failure == null
                    ? "Startup failed."
                    : $"Required startup task '{failure.TaskId}' failed in stage {failure.Stage} after {failure.Attempts} attempt(s): {failure.Exception.Message}",
                failure?.Exception)
        {
            Failure = failure ?? throw new ArgumentNullException(nameof(failure));
        }

        public StartupFailure Failure { get; }
    }
}
