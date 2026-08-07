using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Base.Audio;
using Base.UI;
using Base.Pooling;
using Base.Persistence;
using Base.Common;
using Base.IAP;
using Base.Assets;

namespace Base.Bootstrap
{
    /// <summary>
    /// Root composition root. Runtime dependencies are explicit and the core package does not
    /// require Odin, DOTween, URP or Addressables.
    /// </summary>
    public class RootLifetimeScope : LifetimeScope
    {
        [Header("Runtime Managers")]
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private InputManager inputManager;
        [SerializeField] private GameSceneManager gameSceneManager;
        [SerializeField] private PoolManager poolManager;
        [SerializeField] private HapticManager hapticManager;

        [Header("Shared Catalogs (Optional)")]
        [SerializeField] private AudioLibrarySO audioLibrary;
        [SerializeField] private UICatalogSO uiCatalog;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPointExceptionHandler(exception =>
            {
                BaseLog.LogError(
                    $"[VContainer Exception] {exception.Message}\n{exception.StackTrace}");
            });
            EntryPointsBuilder.EnsureDispatcherRegistered(builder);

            RegisterSharedAssets(builder);
            RegisterCoreServices(builder);
            RegisterProcedures(builder);
            RegisterProcedureManager(builder);
            RegisterIAP(builder);
            RegisterComponents(builder);
        }

        /// <summary>
        /// Override in the application composition root and explicitly register each procedure:
        /// builder.Register&lt;MenuProcedure&gt;(Lifetime.Singleton).As&lt;Procedure&gt;().AsSelf();
        /// </summary>
        protected virtual void RegisterProcedures(IContainerBuilder builder)
        {
        }

        protected virtual void RegisterIAP(IContainerBuilder builder)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.Register<SimulatedIAPProvider>(Lifetime.Singleton)
                .As<IIAPProvider>();
#else
            builder.Register(
                    _ => new UnavailableIAPProvider(
                        "No production IAP provider is configured."),
                    Lifetime.Singleton)
                .As<IIAPProvider>();
#endif
            builder.Register<IAPService>(Lifetime.Singleton).AsSelf();
        }

        protected virtual void RegisterAssetProvider(IContainerBuilder builder)
        {
            builder.Register<ResourcesAssetProvider>(Lifetime.Singleton)
                .As<IAssetProvider>()
                .As<IDisposable>();
        }

        private void RegisterSharedAssets(IContainerBuilder builder)
        {
            if (audioLibrary != null)
            {
                builder.RegisterInstance(audioLibrary);
            }

            if (uiCatalog != null)
            {
                builder.RegisterInstance(uiCatalog);
            }
        }

        private void RegisterCoreServices(IContainerBuilder builder)
        {
            RegisterAssetProvider(builder);

            builder.Register<SettingsManager>(Lifetime.Singleton)
                .AsSelf()
                .As<ISettingsProvider>()
                .As<ISettingsService>()
                .As<IInitializable>()
                .As<IDisposable>();
        }

        private static void RegisterProcedureManager(IContainerBuilder builder)
        {
            builder.Register<ProcedureManager>(Lifetime.Singleton)
                .AsSelf()
                .As<IProcedureService>()
                .As<IDisposable>();
        }

        private void RegisterComponents(IContainerBuilder builder)
        {
            if (audioManager != null)
            {
                builder.RegisterComponent(audioManager)
                    .AsSelf()
                    .As<IAudioService>()
                    .As<IInitializable>()
                    .As<IDisposable>();
            }

            if (uiManager != null)
            {
                builder.RegisterComponent(uiManager)
                    .AsSelf()
                    .As<IUIService>()
                    .As<IInitializable>();
            }

            if (inputManager != null)
            {
                builder.RegisterComponent(inputManager)
                    .AsSelf()
                    .As<IInitializable>()
                    .As<IDisposable>();
            }

            if (gameSceneManager != null)
            {
                builder.RegisterComponent(gameSceneManager)
                    .AsSelf()
                    .As<IProcedureSceneLoader>()
                    .As<IInitializable>();
            }

            if (poolManager != null)
            {
                builder.RegisterComponent(poolManager)
                    .AsSelf()
                    .As<IPoolService>()
                    .As<IInitializable>()
                    .As<IDisposable>();
            }

            if (hapticManager != null)
            {
                builder.RegisterComponent(hapticManager)
                    .AsSelf()
                    .As<IHapticService>()
                    .As<IInitializable>()
                    .As<IDisposable>();
            }
        }
    }
}
