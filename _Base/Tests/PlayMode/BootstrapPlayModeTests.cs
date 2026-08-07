using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using Base.Assets;
using Base.Audio;
using Base.Bootstrap;
using Base.Common;
using Base.Persistence;
using Base.Pooling;
using Base.UI;

namespace Base.Tests
{
    public sealed class BootstrapPlayModeTests
    {
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
                Object.Destroy(root);
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
