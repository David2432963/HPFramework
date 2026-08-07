using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// Base class for an explicitly registered game lifecycle state.
/// Procedure instances are created by VContainer and injected through IEnumerable&lt;Procedure&gt;.
/// </summary>
public abstract class Procedure
{
    protected IProcedureService Owner { get; private set; }

    internal void Initialize(IProcedureService owner)
    {
        Owner = owner;
    }

    public virtual void OnEnter() { }
    public virtual void OnExit() { }

    public virtual UniTask OnEnterAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OnEnter();
        return UniTask.CompletedTask;
    }

    public virtual UniTask OnExitAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OnExit();
        return UniTask.CompletedTask;
    }
}
