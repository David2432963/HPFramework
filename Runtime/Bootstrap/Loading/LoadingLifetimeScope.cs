using HP.Framework.Bootstrap;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace HP.Framework.Bootstrap.Loading
{
    /// <summary>
    /// LifetimeScope for LoadingScene. Auto-child of RootLifetimeScope.
    /// Injects shared services (GameSceneManager) into LoadingScreen.
    /// </summary>
    public class LoadingLifetimeScope : BaseSceneLifetimeScope
    {
        [SerializeField] private LoadingScreen loadingScreen;

        protected override void RegisterComponents(IContainerBuilder builder)
        {
            if (loadingScreen != null)
            {
                builder.RegisterComponent(loadingScreen);
            }
        }
    }
}

