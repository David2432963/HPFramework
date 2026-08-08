using HP.Framework.Bootstrap;
using VContainer;

namespace HP.Framework.Samples.BasicSetup
{
    public sealed class SampleGameLifetimeScope : RootLifetimeScope
    {
        protected override void RegisterApplicationServices(IContainerBuilder builder)
        {
            builder.Register<SampleProfileService>(Lifetime.Singleton).AsSelf();
        }
    }

    public sealed class SampleProfileService
    {
    }
}
