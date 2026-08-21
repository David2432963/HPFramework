using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using HP.Framework.Assets;
using HP.Framework.Audio;
using HP.Framework.Common;
using HP.Framework.Haptics;
using HP.Framework.Lifecycle;
using HP.Framework.Persistence;
using HP.Framework.Pooling;
using HP.Framework.UI;

namespace HP.Framework.Bootstrap
{
    /// <summary>
    /// Root composition root. Runtime dependencies are explicit and the core package does not
    /// require Odin, DOTween, URP or Addressables.
    /// </summary>
    [DefaultExecutionOrder(-6000)]
    public class RootLifetimeScope : LifetimeScope
    {
        [Header("Runtime Managers")]
        [SerializeField] private ApplicationLifecycleService applicationLifecycle;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private UIManager uiManager;
        [Tooltip("Optional input component. HP Framework InputManager is registered when the Input System module is available.")]
        [SerializeField] private MonoBehaviour inputManager;
        [SerializeField] private GameSceneManager gameSceneManager;
        [SerializeField] private PoolManager poolManager;
        [SerializeField] private HapticManager hapticManager;

        [Header("Shared Catalogs (Optional)")]
        [SerializeField] private AudioLibrarySO audioLibrary;
        [SerializeField] private UICatalogSO uiCatalog;

        protected override void Awake()
        {
            DontDestroyOnLoad(gameObject);
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(this).As<RootLifetimeScope>();
            builder.RegisterEntryPointExceptionHandler(exception =>
            {
                BaseLog.LogError(
                    $"[VContainer Exception] {exception.Message}\n{exception.StackTrace}");
            });
            EntryPointsBuilder.EnsureDispatcherRegistered(builder);
            builder.RegisterEntryPoint<FrameworkTickDispatcher>();

            RegisterSharedAssets(builder);
            RegisterCoreServices(builder);
            RegisterProcedures(builder);
            RegisterProcedureManager(builder);
            RegisterComponents(builder);
            RegisterApplicationServices(builder);
            RegisterApplicationComponents(builder);
            RegisterApplicationEntryPoints(builder);
        }

        /// <summary>
        /// Override in the application composition root and explicitly register application-flow
        /// procedures (Boot/MainMenu/Loading/session boundaries). Do not put scene gameplay systems
        /// here; register those as scene/feature services or entry points instead.
        /// builder.Register&lt;MenuProcedure&gt;(Lifetime.Singleton).As&lt;Procedure&gt;().AsSelf();
        /// </summary>
        protected virtual void RegisterProcedures(IContainerBuilder builder)
        {
        }

        /// <summary>
        /// Register game/application-owned plain C# services here. Keep global mutable state
        /// explicit and prefer scene/feature scopes for dependencies that do not need app lifetime.
        /// </summary>
        protected virtual void RegisterApplicationServices(IContainerBuilder builder)
        {
        }

        /// <summary>
        /// Register application-owned MonoBehaviour components that already exist in the root
        /// hierarchy or are intentionally owned for the whole app lifetime.
        /// </summary>
        protected virtual void RegisterApplicationComponents(IContainerBuilder builder)
        {
        }

        /// <summary>
        /// Register app-level VContainer entry points here. Gameplay/menu loops should normally
        /// live in their scene scope instead of the root scope.
        /// </summary>
        protected virtual void RegisterApplicationEntryPoints(IContainerBuilder builder)
        {
        }

        protected virtual void RegisterAssetProvider(IContainerBuilder builder)
        {
            builder.Register<ResourcesAssetProvider>(Lifetime.Singleton)
                .AsSelf()
                .As<IAssetProvider>()
                .As<IAssetLeaseProvider>()
                .As<IAssetMemoryService>()
                .As<IAssetDiagnostics>()
                .As<IDisposable>();

            RegisterAssetMemoryLifecycleIntegration(builder);
        }

        /// <summary>
        /// Optional helper for custom asset-provider registrations. Call this after registering an
        /// IAssetMemoryService when the custom provider should trim zero-reference records on the
        /// framework low-memory signal.
        /// </summary>
        protected void RegisterAssetMemoryLifecycleIntegration(IContainerBuilder builder)
        {
            if (applicationLifecycle == null)
            {
                return;
            }

            builder.Register<AssetMemoryLifecycleTrimmer>(Lifetime.Singleton)
                .As<IInitializable>()
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
            builder.RegisterScopeEventBus();
            RegisterAssetProvider(builder);
            RegisterSettingsServices(builder);
        }

        /// <summary>
        /// Override when a game owns a different settings model/storage. The default registers
        /// PlayerPrefsSettingsStore plus the compatibility SettingsManager.
        /// </summary>
        protected virtual void RegisterSettingsServices(IContainerBuilder builder)
        {
            builder.Register<PlayerPrefsSettingsStore>(Lifetime.Singleton)
                .As<ISettingsStore>();

            builder.Register<SettingsManager>(Lifetime.Singleton)
                .AsSelf()
                .As<ISettingsProvider>()
                .As<ISettingsService>()
                .As<ISettingsFlushService>()
                .As<IInitializable>()
                .As<IDisposable>();

            if (applicationLifecycle != null)
            {
                builder.Register<ApplicationLifecycleSettingsFlush>(Lifetime.Singleton)
                    .As<IInitializable>()
                    .As<IDisposable>();
            }
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
            if (applicationLifecycle != null)
            {
                builder.RegisterComponent(applicationLifecycle)
                    .AsSelf()
                    .As<IApplicationLifecycle>()
                    .As<IDisposable>();
            }

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
                    .As<IGlobalUIService>()
                    .As<IInitializable>();
            }

            if (inputManager != null)
            {
                builder.RegisterComponent(inputManager)
                    .AsSelf()
                    .AsImplementedInterfaces();
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

