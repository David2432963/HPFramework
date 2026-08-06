using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// Base class for game lifecycle procedure states.
/// Supports both synchronous OnEnter/OnExit and UniTask asynchronous OnEnterAsync/OnExitAsync.
/// </summary>
public abstract class Procedure
{
    protected ProcedureManager Owner { get; private set; }

    public void Initialize(ProcedureManager owner)
    {
        Owner = owner;
    }

    public virtual void OnEnter() { }
    public virtual void OnExit() { }

    public virtual UniTask OnEnterAsync(CancellationToken cancellationToken = default)
    {
        OnEnter();
        return UniTask.CompletedTask;
    }

    public virtual UniTask OnExitAsync(CancellationToken cancellationToken = default)
    {
        OnExit();
        return UniTask.CompletedTask;
    }
}
