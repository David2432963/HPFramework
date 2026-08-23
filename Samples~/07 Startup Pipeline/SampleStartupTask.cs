using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HP.Framework.Bootstrap;
using HP.Framework.Startup;
using VContainer;

namespace HP.Framework.Samples.StartupPipeline
{
    public sealed class SampleStartupTask : IStartupTask
    {
        public string Id => "sample.remote-config";
        public StartupTaskCriticality Criticality => StartupTaskCriticality.Required;

        public async UniTask ExecuteAsync(
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            progress?.Report(0f);
            await UniTask.Delay(100, cancellationToken: cancellationToken);
            progress?.Report(1f);
        }
    }

    public sealed class SampleStartupLifetimeScope : RootLifetimeScope
    {
        protected override void RegisterApplicationStartupTasks(IContainerBuilder builder)
        {
            builder.RegisterStartupTask<SampleStartupTask>(
                stage: 0,
                timeout: TimeSpan.FromSeconds(5),
                retryCount: 1,
                retryDelay: TimeSpan.FromMilliseconds(250));
        }
    }
}
