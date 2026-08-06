using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Base.UI
{
    /// <summary>
    /// LifetimeScope for LoadingScene. Auto-child of RootLifetimeScope.
    /// Injects shared services (GameSceneManager) into LoadingScreen.
    /// </summary>
    public class LoadingLifetimeScope : LifetimeScope
    {
        [SerializeField] private LoadingScreen loadingScreen;

        protected override void Configure(IContainerBuilder builder)
        {
            if (loadingScreen != null)
            {
                builder.RegisterComponent(loadingScreen);
            }
        }
    }
}
