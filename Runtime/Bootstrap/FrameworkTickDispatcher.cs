using System;
using HP.Framework.Animations;
using VContainer.Unity;

namespace HP.Framework.Bootstrap
{
    internal sealed class FrameworkTickDispatcher : IInitializable, ITickable, IDisposable
    {
        private bool ownsDriver;

        public void Initialize()
        {
            ownsDriver = FrameworkTickRegistry.AttachDriver(this);
        }

        public void Tick()
        {
            if (ownsDriver)
            {
                FrameworkTickRegistry.Dispatch(this);
            }
        }

        public void Dispose()
        {
            if (!ownsDriver)
            {
                return;
            }

            ownsDriver = false;
            FrameworkTickRegistry.DetachDriver(this);
        }
    }
}
