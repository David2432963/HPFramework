using HP.Framework.Bootstrap;
using VContainer;

namespace HP.Framework.Samples.GameplayScope
{
    public sealed class SampleGameplayLifetimeScope : BaseSceneLifetimeScope
    {
        protected override void RegisterServices(IContainerBuilder builder)
        {
            builder.Register<SampleGameplaySession>(Lifetime.Scoped).AsSelf();
        }
    }

    public sealed class SampleGameplaySession
    {
    }
}
