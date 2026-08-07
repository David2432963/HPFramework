using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Base;
using UnityEngine.SceneManagement;

public enum ProcedureTransitionState
{
    Idle,
    Exiting,
    Entering,
    Failed
}

/// <summary>
/// Explicit procedure state machine. Procedures must be registered in a LifetimeScope as Procedure.
/// No assembly scanning, Activator fallback or service locator is used.
/// </summary>
public sealed class ProcedureManager : IProcedureService, IDisposable
{
    private readonly Dictionary<Type, Procedure> procedures = new Dictionary<Type, Procedure>();
    private readonly SemaphoreSlim transitionGate = new SemaphoreSlim(1, 1);

    private IProcedureSceneLoader sceneLoader;
    private Procedure currentProcedure;
    private bool disposed;

    private string targetSceneName;
    private Type targetProcedureType;
    private float targetFakeLoadingDuration;

    public Procedure CurrentProcedure => currentProcedure;
    public ProcedureTransitionState TransitionState { get; private set; } = ProcedureTransitionState.Idle;
    public Exception LastTransitionException { get; private set; }

    public string TargetSceneName => targetSceneName;
    public Type TargetProcedureType => targetProcedureType;
    public float TargetFakeLoadingDuration => targetFakeLoadingDuration;

    public event Action<Type, Type> TransitionStarted;
    public event Action<Procedure> TransitionCompleted;
    public event Action<Type, Exception> TransitionFailed;

    public ProcedureManager(IEnumerable<Procedure> registeredProcedures)
    {
        if (registeredProcedures == null)
        {
            return;
        }

        foreach (Procedure procedure in registeredProcedures)
        {
            RegisterProcedure(procedure);
        }
    }

    public void RegisterSceneLoader(IProcedureSceneLoader loader)
    {
        ThrowIfDisposed();
        sceneLoader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    public void RegisterProcedure(Procedure procedure)
    {
        ThrowIfDisposed();
        if (procedure == null)
        {
            throw new ArgumentNullException(nameof(procedure));
        }

        Type type = procedure.GetType();
        if (procedures.ContainsKey(type))
        {
            throw new InvalidOperationException($"Procedure '{type.FullName}' is already registered.");
        }

        procedure.Initialize(this);
        procedures.Add(type, procedure);
    }

    public bool IsRegistered<T>() where T : Procedure
    {
        return procedures.ContainsKey(typeof(T));
    }

    public void ChangeState<T>() where T : Procedure
    {
        ChangeState(typeof(T));
    }

    public void ChangeState(Type procedureType)
    {
        ThrowIfDisposed();
        Procedure nextProcedure = GetRequiredProcedure(procedureType);
        if (ReferenceEquals(currentProcedure, nextProcedure))
        {
            return;
        }

        if (TransitionState == ProcedureTransitionState.Exiting || TransitionState == ProcedureTransitionState.Entering)
        {
            throw new InvalidOperationException("A procedure transition is already running. Use ChangeStateAsync to queue transitions safely.");
        }

        Procedure previous = currentProcedure;
        LastTransitionException = null;
        TransitionStarted?.Invoke(previous?.GetType(), procedureType);

        try
        {
            TransitionState = ProcedureTransitionState.Exiting;
            previous?.OnExit();

            TransitionState = ProcedureTransitionState.Entering;
            currentProcedure = nextProcedure;
            nextProcedure.OnEnter();

            TransitionState = ProcedureTransitionState.Idle;
            TransitionCompleted?.Invoke(nextProcedure);
            BaseLog.Log($"[ProcedureManager] Transitioned to state: {procedureType.Name}");
        }
        catch (Exception exception)
        {
            currentProcedure = previous;
            LastTransitionException = exception;
            TransitionState = ProcedureTransitionState.Failed;
            TransitionFailed?.Invoke(procedureType, exception);
            throw;
        }
    }

    public UniTask ChangeStateAsync<T>(CancellationToken cancellationToken = default) where T : Procedure
    {
        return ChangeStateAsync(typeof(T), cancellationToken);
    }

    public async UniTask ChangeStateAsync(Type procedureType, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Procedure nextProcedure = GetRequiredProcedure(procedureType);
        if (ReferenceEquals(currentProcedure, nextProcedure))
        {
            return;
        }

        await transitionGate.WaitAsync(cancellationToken);
        Procedure previous = currentProcedure;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ReferenceEquals(currentProcedure, nextProcedure))
            {
                return;
            }

            previous = currentProcedure;
            LastTransitionException = null;
            TransitionStarted?.Invoke(previous?.GetType(), procedureType);

            TransitionState = ProcedureTransitionState.Exiting;
            if (previous != null)
            {
                await previous.OnExitAsync(cancellationToken);
            }

            TransitionState = ProcedureTransitionState.Entering;
            currentProcedure = nextProcedure;
            await nextProcedure.OnEnterAsync(cancellationToken);

            TransitionState = ProcedureTransitionState.Idle;
            TransitionCompleted?.Invoke(nextProcedure);
            BaseLog.Log($"[ProcedureManager] Transitioned to state: {procedureType.Name}");
        }
        catch (Exception exception)
        {
            currentProcedure = previous;
            LastTransitionException = exception;
            TransitionState = ProcedureTransitionState.Failed;
            TransitionFailed?.Invoke(procedureType, exception);
            throw;
        }
        finally
        {
            transitionGate.Release();
        }
    }

    public void LoadProcedure<TTarget, TLoading>(string sceneName, float fakeLoadingDuration = 0f)
        where TTarget : Procedure
        where TLoading : Procedure
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            throw new ArgumentException("A target scene name is required.", nameof(sceneName));
        }

        if (!IsRegistered<TTarget>())
        {
            throw new InvalidOperationException($"Target procedure '{typeof(TTarget).FullName}' is not registered.");
        }

        targetSceneName = sceneName;
        targetProcedureType = typeof(TTarget);
        targetFakeLoadingDuration = Math.Max(0f, fakeLoadingDuration);
        ChangeState<TLoading>();
    }

    public async UniTask<Scene> LoadTargetSceneAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (sceneLoader == null)
        {
            throw new InvalidOperationException("A procedure scene loader must be registered before loading a target scene.");
        }

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            throw new InvalidOperationException("A target scene must be assigned before entering the loading procedure.");
        }

        return await sceneLoader.LoadSceneAsync(targetSceneName, targetFakeLoadingDuration, cancellationToken);
    }

    public UniTask EnterTargetProcedureAsync(CancellationToken cancellationToken = default)
    {
        if (targetProcedureType == null)
        {
            throw new InvalidOperationException("No target procedure has been configured.");
        }

        return ChangeStateAsync(targetProcedureType, cancellationToken);
    }

    private Procedure GetRequiredProcedure(Type procedureType)
    {
        if (procedureType == null)
        {
            throw new ArgumentNullException(nameof(procedureType));
        }

        if (!typeof(Procedure).IsAssignableFrom(procedureType))
        {
            throw new ArgumentException($"Type '{procedureType.FullName}' is not a Procedure.", nameof(procedureType));
        }

        if (!procedures.TryGetValue(procedureType, out Procedure procedure))
        {
            throw new InvalidOperationException(
                $"Procedure '{procedureType.FullName}' is not registered. Register it with builder.Register<T>(Lifetime.Singleton).As<Procedure>().AsSelf().");
        }

        return procedure;
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(ProcedureManager));
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        transitionGate.Dispose();
        procedures.Clear();
        sceneLoader = null;
        currentProcedure = null;
    }
}
