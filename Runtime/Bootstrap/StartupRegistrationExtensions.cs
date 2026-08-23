using System;
using HP.Framework.Startup;
using VContainer;

namespace HP.Framework.Bootstrap
{
    public static class StartupRegistrationExtensions
    {
        public static void RegisterStartupTask<TTask>(
            this IContainerBuilder builder,
            int stage,
            TimeSpan timeout,
            int retryCount = 0,
            TimeSpan retryDelay = default)
            where TTask : class, IStartupTask
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var options = new StartupTaskOptions(stage, timeout, retryCount, retryDelay);
            builder.Register<TTask>(Lifetime.Singleton).AsSelf();
            builder.Register<StartupTaskRegistration<TTask>>(
                    resolver => new StartupTaskRegistration<TTask>(resolver.Resolve<TTask>(), options),
                    Lifetime.Singleton)
                .As<IStartupTaskRegistration>();
        }

        public static void RegisterStartupTask<TTask>(
            this IContainerBuilder builder,
            int stage)
            where TTask : class, IStartupTask
        {
            StartupTaskOptions options = StartupTaskOptions.Default(stage);
            builder.RegisterStartupTask<TTask>(
                options.Stage,
                options.Timeout,
                options.RetryCount,
                options.RetryDelay);
        }
    }
}
