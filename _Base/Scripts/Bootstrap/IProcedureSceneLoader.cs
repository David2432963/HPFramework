using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

public interface IProcedureSceneLoader
{
    UniTask<Scene> LoadSceneAsync(
        string sceneName,
        float fakeLoadingDuration = 0f,
        CancellationToken cancellationToken = default);
}
