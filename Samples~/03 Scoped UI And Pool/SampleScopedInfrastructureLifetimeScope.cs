using HP.Framework.Bootstrap;
using HP.Framework.Pooling;
using HP.Framework.UI;
using VContainer;

namespace HP.Framework.Samples.ScopedInfrastructure
{
    public sealed class SampleScopedInfrastructureLifetimeScope : BaseSceneLifetimeScope
    {
        protected override void RegisterServices(IContainerBuilder builder)
        {
            builder.RegisterScopedPool();
            builder.RegisterScopedUI();
        }
    }
}
