using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using HP.Framework.Assets;
using HP.Framework.Audio;
using HP.Framework.Bootstrap;
using HP.Framework.Common;
using HP.Framework.Haptics;
using HP.Framework.Input;
using HP.Framework.Persistence;
using HP.Framework.Pooling;
using HP.Framework.UI;

namespace HP.Framework.Tests
{
    public sealed class ScopeTestDependency : System.IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    public sealed class ScopedPoolInjectionProbe : MonoBehaviour
    {
        public ScopeTestDependency Dependency { get; private set; }

        [Inject]
        public void Construct(ScopeTestDependency dependency)
        {
            Dependency = dependency;
        }
    }

    public sealed class ScopedPopupInjectionProbe : BasePopup
    {
        public ScopeTestDependency Dependency { get; private set; }
        public IUIService InjectedUIService => uiService;

        [Inject]
        public void ConstructScoped(ScopeTestDependency dependency)
        {
            Dependency = dependency;
        }
    }

    public sealed class ScopeArchitecturePlayModeTests
    {
        public sealed class ScopedMarker : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        [UnityTest]
        public IEnumerator RootSceneFeatureScopes_InheritDependencies_AndDisposeSceneOwnedState()
        {
            GameObject rootObject = new GameObject("ScopeArchitectureTest");
            rootObject.SetActive(false);
            RootLifetimeScope rootScope = rootObject.AddComponent<RootLifetimeScope>();
            rootScope.autoRun = false;
            rootObject.SetActive(true);
            rootScope.Build();

            ISettingsService rootSettings = rootScope.Container.Resolve<ISettingsService>();
            IEventBus rootEventBus = rootScope.Container.Resolve<IEventBus>();

            BaseSceneLifetimeScope sceneScope = rootScope.CreateChild<BaseSceneLifetimeScope>(
                builder => builder.Register<ScopedMarker>(Lifetime.Singleton).AsSelf(),
                "SceneScope");
            ScopedMarker sceneMarker = sceneScope.Container.Resolve<ScopedMarker>();

            BaseFeatureLifetimeScope featureScope = sceneScope.CreateChild<BaseFeatureLifetimeScope>(
                builder => { },
                "FeatureScope");

            Assert.That(sceneScope.Parent, Is.SameAs(rootScope));
            Assert.That(featureScope.Parent, Is.SameAs(sceneScope));
            Assert.That(featureScope.Container.Resolve<ISettingsService>(), Is.SameAs(rootSettings));
            Assert.That(featureScope.Container.Resolve<IEventBus>(), Is.SameAs(rootEventBus));
            Assert.That(featureScope.Container.Resolve<ScopedMarker>(), Is.SameAs(sceneMarker));

            UnityEngine.Object.Destroy(sceneScope.gameObject);
            yield return null;
            Assert.That(sceneMarker.IsDisposed, Is.True);

            UnityEngine.Object.Destroy(rootObject);
            yield return null;
        }
    }

    public sealed class BootstrapPlayModeTests
    {
        [UnityTest]
        public IEnumerator ChildScope_CanShadowUIAndPool_WithItsOwnResolver()
        {
            GameObject root = CreateConfiguredBootstrap();
            GameObject pooledPrefab = null;
            GameObject popupPrefabObject = null;
            try
            {
                root.SetActive(true);
                yield return null;

                RootLifetimeScope rootScope = root.GetComponent<RootLifetimeScope>();
                IUIService rootUi = rootScope.Container.Resolve<IUIService>();
                IPoolService rootPool = rootScope.Container.Resolve<IPoolService>();
                IEventBus rootEventBus = rootScope.Container.Resolve<IEventBus>();

                BaseSceneLifetimeScope sceneScope =
                    rootScope.CreateChild<BaseSceneLifetimeScope>(builder =>
                    {
                        builder.Register<ScopeTestDependency>(Lifetime.Scoped).AsSelf();
                        builder.RegisterScopeEventBus();
                        builder.RegisterScopedPool();
                        builder.RegisterScopedUI();
                    }, "ScopedInfrastructureTest");

                IUIService sceneUi = sceneScope.Container.Resolve<IUIService>();
                IPoolService scenePool = sceneScope.Container.Resolve<IPoolService>();
                IScopedUIService scopedUi = sceneScope.Container.Resolve<IScopedUIService>();
                IEventBus sceneEventBus = sceneScope.Container.Resolve<IEventBus>();
                ScopeTestDependency dependency =
                    sceneScope.Container.Resolve<ScopeTestDependency>();

                Assert.That(sceneUi, Is.TypeOf<ScopedUIService>());
                Assert.That(scenePool, Is.TypeOf<ScopedPoolService>());
                Assert.That(sceneUi, Is.Not.SameAs(rootUi));
                Assert.That(scenePool, Is.Not.SameAs(rootPool));
                Assert.That(sceneEventBus, Is.Not.SameAs(rootEventBus));

                pooledPrefab = new GameObject("InjectedPoolProbePrefab");
                pooledPrefab.SetActive(false);
                pooledPrefab.AddComponent<ScopedPoolInjectionProbe>();
                GameObject pooledInstance = scenePool.Get(pooledPrefab);
                ScopedPoolInjectionProbe pooledProbe =
                    pooledInstance.GetComponent<ScopedPoolInjectionProbe>();
                Assert.That(pooledProbe.Dependency, Is.SameAs(dependency));
                scenePool.Release(pooledInstance);

                popupPrefabObject = new GameObject(
                    "ScopedPopupProbePrefab",
                    typeof(RectTransform),
                    typeof(CanvasGroup));
                popupPrefabObject.SetActive(false);
                ScopedPopupInjectionProbe popupPrefab =
                    popupPrefabObject.AddComponent<ScopedPopupInjectionProbe>();

                ScopedPopupInjectionProbe popup = scopedUi.OpenPopup(popupPrefab);
                Assert.That(popup, Is.Not.Null);
                Assert.That(popup.Dependency, Is.SameAs(dependency));
                Assert.That(popup.InjectedUIService, Is.SameAs(sceneUi));

                UnityEngine.Object.Destroy(sceneScope.gameObject);
                yield return null;

                Assert.That(dependency.IsDisposed, Is.True);
                Assert.That(popup == null, Is.True,
                    "Scope-owned popup should be destroyed with the child scope.");
            }
            finally
            {
                if (pooledPrefab != null)
                {
                    UnityEngine.Object.Destroy(pooledPrefab);
                }

                if (popupPrefabObject != null)
                {
                    UnityEngine.Object.Destroy(popupPrefabObject);
                }

                UnityEngine.Object.Destroy(root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Bootstrap_BuildsContainer_AndResolvesCoreServices()
        {
            GameObject root = CreateConfiguredBootstrap();
            try
            {
                root.SetActive(true);
                yield return null;

                RootLifetimeScope scope = root.GetComponent<RootLifetimeScope>();
                Assert.That(scope.Container, Is.Not.Null);

                Assert.That(scope.Container.Resolve<ISettingsService>(), Is.Not.Null);
                Assert.That(scope.Container.Resolve<IEventBus>(), Is.Not.Null);
                Assert.That(scope.Container.Resolve<IProcedureService>(), Is.Not.Null);
                Assert.That(scope.Container.Resolve<IAssetProvider>(), Is.Not.Null);
                Assert.That(scope.Container.Resolve<IAudioService>(), Is.Not.Null);
                Assert.That(scope.Container.Resolve<IUIService>(), Is.Not.Null);
                Assert.That(scope.Container.Resolve<IPoolService>(), Is.Not.Null);
                Assert.That(scope.Container.Resolve<IHapticService>(), Is.Not.Null);
                Assert.That(scope.Container.Resolve<GameSceneManager>(), Is.Not.Null);
                Assert.That(scope.Container.Resolve<InputManager>(), Is.Not.Null);

                SettingsManager settings = scope.Container.Resolve<SettingsManager>();
                Assert.That(settings.IsInitialized, Is.True);

                GameSceneManager sceneManager = scope.Container.Resolve<GameSceneManager>();
                Assert.That(sceneManager.CurrentSceneName, Is.Not.Null.And.Not.Empty);
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }

            yield return null;
        }

        private static GameObject CreateConfiguredBootstrap()
        {
            GameObject root = new GameObject("BootstrapPlayModeTest");
            root.SetActive(false);

            RootLifetimeScope scope = root.AddComponent<RootLifetimeScope>();
            AudioManager audioManager = root.AddComponent<AudioManager>();
            UIManager uiManager = root.AddComponent<UIManager>();
            InputManager inputManager = root.AddComponent<InputManager>();
            GameSceneManager sceneManager = root.AddComponent<GameSceneManager>();
            PoolManager poolManager = root.AddComponent<PoolManager>();
            HapticManager hapticManager = root.AddComponent<HapticManager>();

            Camera uiCamera = CreateChild(root.transform, "UICamera")
                .gameObject.AddComponent<Camera>();
            RectTransform canvasRoot = CreateRectChild(root.transform, "UICanvas");
            RectTransform screenCanvas = CreateRectChild(canvasRoot, "ScreenCanvas");
            RectTransform popupCanvas = CreateRectChild(canvasRoot, "PopupCanvas");
            RectTransform notificationCanvas = CreateRectChild(canvasRoot, "NotificationCanvas");
            RectTransform lockCanvas = CreateRectChild(canvasRoot, "LockCanvas");
            Transform poolRoot = CreateChild(root.transform, "PoolRoot");

            SetField(scope, "audioManager", audioManager);
            SetField(scope, "uiManager", uiManager);
            SetField(scope, "inputManager", inputManager);
            SetField(scope, "gameSceneManager", sceneManager);
            SetField(scope, "poolManager", poolManager);
            SetField(scope, "hapticManager", hapticManager);

            SetField(uiManager, "screenCanvas", screenCanvas);
            SetField(uiManager, "popupCanvas", popupCanvas);
            SetField(uiManager, "notiCanvas", notificationCanvas);
            SetField(uiManager, "lockCanvas", lockCanvas);
            SetField(uiManager, "uiCamera", uiCamera);
            SetField(poolManager, "rootPoolParent", poolRoot);

            return root;
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static RectTransform CreateRectChild(Transform parent, string name)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rectTransform = child.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Serialized field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }
    }
}


