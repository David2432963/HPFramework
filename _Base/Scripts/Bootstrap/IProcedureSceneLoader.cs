using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public interface IProcedureSceneLoader
{
    Coroutine LoadSceneAsync(string sceneName, float fakeLoadingDuration = 0f, Action<Scene> onLoaded = null);
}
