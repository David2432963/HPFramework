using System;
using VContainer;

namespace HP.Framework.UI
{
    /// <summary>
    /// VContainer registrations for scene/feature-owned UI.
    /// </summary>
    public static class UIVContainerExtensions
    {
        /// <summary>
        /// Shadows the root IUIService in the current child scope. Local views registered through
        /// IScopedUIService are created with the child resolver and cleaned up on scope disposal.
        /// </summary>
        public static RegistrationBuilder RegisterScopedUI(
            this IContainerBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.Register<ScopedUIService>(Lifetime.Scoped)
                .AsSelf()
                .As<IUIService>()
                .As<IScopedUIService>()
                .As<IDisposable>();
        }
    }
}


