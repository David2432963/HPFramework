using HP.Framework.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HP.Framework.Tests
{
    public sealed class InputManagerTests
    {
        [Test]
        public void AdditionalMap_CoexistsWithPrimary_AndCanBeReleased()
        {
            GameObject root = new GameObject("InputManagerTest");
            InputActionAsset actions = ScriptableObject.CreateInstance<InputActionAsset>();
            InputManager manager = root.AddComponent<InputManager>();
            InputActionMap ui = CreateMap(actions, "UI");
            InputActionMap player = CreateMap(actions, "Player");

            try
            {
                manager.SetInputActions(actions, "UI");

                Assert.That(manager.CurrentMapName, Is.EqualTo("UI"));
                Assert.That(ui.enabled, Is.True);
                Assert.That(manager.TryEnableAdditionalMap("Player"), Is.True);
                Assert.That(player.enabled, Is.True);
                Assert.That(ui.enabled, Is.True);
                Assert.That(manager.CurrentMapName, Is.EqualTo("UI"));
                Assert.That(manager.IsMapEnabled("Player"), Is.True);

                Assert.That(manager.TryDisableAdditionalMap("Player"), Is.True);
                Assert.That(player.enabled, Is.False);
                Assert.That(ui.enabled, Is.True);
            }
            finally
            {
                manager.Dispose();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(actions);
            }
        }

        [Test]
        public void SwitchingPrimary_PreservesTrackedAdditionalMaps()
        {
            GameObject root = new GameObject("InputManagerSwitchTest");
            InputActionAsset actions = ScriptableObject.CreateInstance<InputActionAsset>();
            InputManager manager = root.AddComponent<InputManager>();
            InputActionMap ui = CreateMap(actions, "UI");
            InputActionMap player = CreateMap(actions, "Player");
            InputActionMap menu = CreateMap(actions, "Menu");

            try
            {
                manager.SetInputActions(actions, "UI");
                Assert.That(manager.TryEnableAdditionalMap("Player"), Is.True);
                Assert.That(manager.TrySwitchMap("Menu"), Is.True);

                Assert.That(ui.enabled, Is.False);
                Assert.That(menu.enabled, Is.True);
                Assert.That(player.enabled, Is.True);
                Assert.That(manager.CurrentMapName, Is.EqualTo("Menu"));
            }
            finally
            {
                manager.Dispose();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(actions);
            }
        }

        [Test]
        public void Dispose_DisablesPrimaryAndAdditionalMaps()
        {
            GameObject root = new GameObject("InputManagerDisposeTest");
            InputActionAsset actions = ScriptableObject.CreateInstance<InputActionAsset>();
            InputManager manager = root.AddComponent<InputManager>();
            InputActionMap ui = CreateMap(actions, "UI");
            InputActionMap player = CreateMap(actions, "Player");

            try
            {
                manager.SetInputActions(actions, "UI");
                Assert.That(manager.TryEnableAdditionalMap("Player"), Is.True);

                manager.Dispose();

                Assert.That(ui.enabled, Is.False);
                Assert.That(player.enabled, Is.False);
                Assert.That(manager.CurrentMapName, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(actions);
            }
        }

        private static InputActionMap CreateMap(InputActionAsset actions, string name)
        {
            InputActionMap map = new InputActionMap(name);
            map.AddAction("Action", InputActionType.Button);
            actions.AddActionMap(map);
            return map;
        }
    }
}
