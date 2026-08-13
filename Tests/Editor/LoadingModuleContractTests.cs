using System;
using System.Linq;
using HP.Framework.Bootstrap;
using HP.Framework.Bootstrap.Loading;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HP.Framework.Tests
{
    public sealed class LoadingModuleContractTests
    {
        private const string LoadingSceneGuid = "9806de10e37b6b04092877dc41f8724d";
        private const string LoadingScenePath =
            "Assets/Plugins/HPFramework/Runtime/Bootstrap/Loading/Scenes/LoadingScene.unity";
        private const string DuplicateSceneFolder = "Assets/HPFrameworkLoadingTests";
        private const string DuplicateScenePath = DuplicateSceneFolder + "/LoadingScene.unity";

        [Test]
        public void LoadingSceneGuid_ResolvesToCanonicalPath()
        {
            Assert.That(AssetDatabase.GUIDToAssetPath(LoadingSceneGuid), Is.EqualTo(LoadingScenePath));
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(LoadingScenePath), Is.Not.Null);
        }

        [Test]
        public void LoadingScene_HasOneScopeAndScreen_WithConfiguredReferences()
        {
            Scene scene = EditorSceneManager.OpenScene(LoadingScenePath, OpenSceneMode.Additive);
            try
            {
                LoadingLifetimeScope[] scopes = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LoadingLifetimeScope>(true))
                    .ToArray();
                LoadingScreen[] screens = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LoadingScreen>(true))
                    .ToArray();

                Assert.That(scopes, Has.Length.EqualTo(1));
                Assert.That(screens, Has.Length.EqualTo(1));

                SerializedObject scopeObject = new SerializedObject(scopes[0]);
                Assert.That(
                    scopeObject.FindProperty("loadingScreen").objectReferenceValue,
                    Is.SameAs(screens[0]));

                SerializedObject screenObject = new SerializedObject(screens[0]);
                Assert.That(
                    screenObject.FindProperty("progressBar").objectReferenceValue,
                    Is.TypeOf<Slider>());
                Assert.That(
                    screenObject.FindProperty("progressText").objectReferenceValue,
                    Is.TypeOf<Text>());
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        [Test]
        public void BuildSettingsRepair_UsesFrameworkSceneAndEnablesIt_WhenDuplicateNameExists()
        {
            EditorBuildSettingsScene[] originalBuildScenes = EditorBuildSettings.scenes;
            try
            {
                if (!AssetDatabase.IsValidFolder(DuplicateSceneFolder))
                {
                    AssetDatabase.CreateFolder("Assets", "HPFrameworkLoadingTests");
                }

                Assert.That(AssetDatabase.CopyAsset(LoadingScenePath, DuplicateScenePath), Is.True);

                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(DuplicateScenePath, true),
                    new EditorBuildSettingsScene(LoadingScenePath, false)
                };

                Assert.That(
                    HP.Framework.Editor.RootLifetimeScopeEditor.EnsureLoadingSceneInBuildSettings(),
                    Is.True);

                EditorBuildSettingsScene[] repairedScenes = EditorBuildSettings.scenes;
                Assert.That(
                    repairedScenes.Single(scene => scene.path == LoadingScenePath).enabled,
                    Is.True);
                Assert.That(
                    repairedScenes.Single(scene => scene.path == DuplicateScenePath).enabled,
                    Is.True);
            }
            finally
            {
                EditorBuildSettings.scenes = originalBuildScenes;
                AssetDatabase.DeleteAsset(DuplicateSceneFolder);
            }
        }

        [Test]
        public void ProjectValidation_FailsWhenLoadingSceneIsDisabled()
        {
            EditorBuildSettingsScene[] originalBuildScenes = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = originalBuildScenes
                    .Select(scene => scene.path == LoadingScenePath
                        ? new EditorBuildSettingsScene(scene.path, false)
                        : scene)
                    .ToArray();

                LogAssert.Expect(
                    LogType.Error,
                    "[HP Framework/VContainer] LoadingScene must be present and enabled in Build Settings. " +
                    "Run Tools/HP Framework/Setup to repair it.");
                Assert.That(
                    HP.Framework.Editor.VContainerProjectSetup.ValidateProjectSetup(logSuccess: false),
                    Is.False);
            }
            finally
            {
                EditorBuildSettings.scenes = originalBuildScenes;
            }
        }

        [Test]
        public void Bootstrap_UsesDefaultLoadingSceneName()
        {
            const string bootstrapGuid = "550d92965c5c4c53b9949039d465faba";
            string bootstrapPath = AssetDatabase.GUIDToAssetPath(bootstrapGuid);
            GameObject bootstrap = AssetDatabase.LoadAssetAtPath<GameObject>(bootstrapPath);
            Assert.That(bootstrap, Is.Not.Null);

            GameSceneManager sceneManager = bootstrap.GetComponentInChildren<GameSceneManager>(true);
            Assert.That(sceneManager, Is.Not.Null);

            SerializedObject managerObject = new SerializedObject(sceneManager);
            Assert.That(
                managerObject.FindProperty("loadingSceneName").stringValue,
                Is.EqualTo(BaseConstants.DefaultLoadingSceneName));
        }
    }
}
