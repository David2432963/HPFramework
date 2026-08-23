#if UNITY_EDITOR
using UnityEditor;

namespace HP.Framework.Editor
{
    /// <summary>
    /// Performs the minimal host-project integration that cannot be serialized inside a package.
    /// Canonical framework assets are already Git-tracked; this hook only keeps LoadingScene
    /// available to SceneManager without requiring every developer to run Setup manually.
    /// </summary>
    [InitializeOnLoad]
    internal static class HPFrameworkAutoIntegration
    {
        private const string SessionKey = "HP.Framework.ZeroSetupIntegrationChecked";

        static HPFrameworkAutoIntegration()
        {
            EditorApplication.delayCall += EnsureProjectIntegration;
        }

        private static void EnsureProjectIntegration()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += EnsureProjectIntegration;
                return;
            }

            SessionState.SetBool(SessionKey, true);
            RootLifetimeScopeEditor.EnsureLoadingSceneInBuildSettings();
        }
    }
}
#endif
