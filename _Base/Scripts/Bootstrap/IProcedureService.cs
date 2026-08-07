using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

public interface IProcedureService
{
    Procedure CurrentProcedure { get; }
    ProcedureTransitionState TransitionState { get; }
    Exception LastTransitionException { get; }

    bool IsRegistered<T>() where T : Procedure;
    void RegisterProcedure(Procedure procedure);
    void ChangeState<T>() where T : Procedure;
    UniTask ChangeStateAsync<T>(CancellationToken cancellationToken = default) where T : Procedure;
    UniTask<Scene> LoadTargetSceneAsync(CancellationToken cancellationToken = default);
}
