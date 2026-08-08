namespace HP.Framework.Bootstrap
{
    using System.Threading;
    using Cysharp.Threading.Tasks;

    /// <summary>
    /// Compatibility base for an explicitly registered application-flow state.
    /// Prefer deriving new root-level flow states from ApplicationProcedure. Scene gameplay logic
    /// belongs in scene/feature scopes and VContainer entry points, not in Procedures.
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


}


