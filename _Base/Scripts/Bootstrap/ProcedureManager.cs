using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using Base;

/// <summary>
/// Game Procedure State Machine managed by VContainer.
/// Uses VContainer IObjectResolver to resolve Procedure instances, allowing Procedure states to use Constructor Injection.
/// </summary>
public sealed class ProcedureManager
{
    private readonly Dictionary<Type, Procedure> procedures = new();
    private readonly IObjectResolver objectResolver;
    private IProcedureSceneLoader sceneLoader;
    private Procedure currentProcedure;

    public Procedure CurrentProcedure => currentProcedure;

    private string targetSceneName;
    private Type targetProcedureType;
    private float targetFakeLoadingDuration;

    public string TargetSceneName => targetSceneName;
    public Type TargetProcedureType => targetProcedureType;
    public float TargetFakeLoadingDuration => targetFakeLoadingDuration;

    public ProcedureManager(IObjectResolver objectResolver, IProcedureSceneLoader sceneLoader = null)
    {
        this.objectResolver = objectResolver;
        this.sceneLoader = sceneLoader;
        RegisterAllProcedures();
    }

    public void RegisterSceneLoader(IProcedureSceneLoader loader)
    {
        if (loader == null)
        {
            throw new ArgumentNullException(nameof(loader));
        }

        sceneLoader = loader;
    }

    public Coroutine LoadTargetSceneAsync(Action<Scene> onLoaded = null)
    {
        if (sceneLoader == null)
        {
            throw new InvalidOperationException("A procedure scene loader must be registered before loading a target scene.");
        }

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            throw new InvalidOperationException("A target scene must be assigned before entering the loading procedure.");
        }

        return sceneLoader.LoadSceneAsync(targetSceneName, targetFakeLoadingDuration, onLoaded);
    }

    public void RegisterProcedure(Procedure procedure)
    {
        if (procedure == null) return;
        var type = procedure.GetType();
        if (!procedures.ContainsKey(type))
        {
            procedure.Initialize(this);
            procedures[type] = procedure;
        }
    }

    public void RegisterProcedure<T>() where T : Procedure
    {
        var type = typeof(T);
        if (!procedures.ContainsKey(type))
        {
            Procedure procedure = null;
            if (objectResolver != null)
            {
                try
                {
                    procedure = objectResolver.Resolve<T>();
                }
                catch
                {
                    // Fallback to activator if not registered in scope
                    procedure = Activator.CreateInstance<T>();
                }
            }
            else
            {
                procedure = Activator.CreateInstance<T>();
            }

            procedure.Initialize(this);
            procedures[type] = procedure;
        }
    }

    public void ChangeState<T>() where T : Procedure
    {
        ChangeState(typeof(T));
    }

    public void ChangeState(Type procedureType)
    {
        if (procedureType == null)
        {
            throw new ArgumentNullException(nameof(procedureType));
        }

        if (currentProcedure != null && currentProcedure.GetType() == procedureType)
        {
            return;
        }

        if (!procedures.TryGetValue(procedureType, out var nextProcedure))
        {
            // Try resolving dynamically via VContainer
            if (objectResolver != null)
            {
                try
                {
                    nextProcedure = (Procedure)objectResolver.Resolve(procedureType);
                    nextProcedure.Initialize(this);
                    procedures[procedureType] = nextProcedure;
                }
                catch (Exception ex)
                {
                    BaseLog.LogError($"[ProcedureManager] Failed to resolve procedure '{procedureType.Name}' via VContainer: {ex.Message}");
                }
            }
        }

        if (nextProcedure != null)
        {
            currentProcedure?.OnExit();
            currentProcedure = nextProcedure;
            BaseLog.Log($"[ProcedureManager] Transitioned to state: {procedureType.Name}");
            currentProcedure.OnEnter();
        }
        else
        {
            BaseLog.LogError($"[ProcedureManager] Procedure of type '{procedureType.Name}' is not registered!");
        }
    }

    public Cysharp.Threading.Tasks.UniTask ChangeStateAsync<T>(System.Threading.CancellationToken cancellationToken = default) where T : Procedure
    {
        return ChangeStateAsync(typeof(T), cancellationToken);
    }

    public async Cysharp.Threading.Tasks.UniTask ChangeStateAsync(Type procedureType, System.Threading.CancellationToken cancellationToken = default)
    {
        if (procedureType == null)
        {
            throw new ArgumentNullException(nameof(procedureType));
        }

        if (currentProcedure != null && currentProcedure.GetType() == procedureType)
        {
            return;
        }

        if (!procedures.TryGetValue(procedureType, out var nextProcedure))
        {
            if (objectResolver != null)
            {
                try
                {
                    nextProcedure = (Procedure)objectResolver.Resolve(procedureType);
                    nextProcedure.Initialize(this);
                    procedures[procedureType] = nextProcedure;
                }
                catch (Exception ex)
                {
                    BaseLog.LogError($"[ProcedureManager] Failed to resolve procedure '{procedureType.Name}' via VContainer: {ex.Message}");
                }
            }
        }

        if (nextProcedure != null)
        {
            if (currentProcedure != null)
            {
                await currentProcedure.OnExitAsync(cancellationToken);
            }
            currentProcedure = nextProcedure;
            BaseLog.Log($"[ProcedureManager] Transitioned to state: {procedureType.Name}");
            await currentProcedure.OnEnterAsync(cancellationToken);
        }
        else
        {
            BaseLog.LogError($"[ProcedureManager] Procedure of type '{procedureType.Name}' is not registered!");
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

        targetSceneName = sceneName;
        targetProcedureType = typeof(TTarget);
        targetFakeLoadingDuration = fakeLoadingDuration;
        ChangeState<TLoading>();
    }

    private void RegisterAllProcedures()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            string name = assembly.FullName;
            if (name.StartsWith("System") || name.StartsWith("Microsoft") || 
                name.StartsWith("Unity") || name.StartsWith("mscorlib") ||
                name.StartsWith("Mono.") || name.StartsWith("Newtonsoft") ||
                name.StartsWith("VContainer") || name.StartsWith("DOTween") ||
                name.StartsWith("netstandard") || name.StartsWith("nunit") ||
                name.StartsWith("ExCSS") || name.StartsWith("JetBrains"))
            {
                continue;
            }

            try
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    if (type.IsClass && !type.IsAbstract && typeof(Procedure).IsAssignableFrom(type))
                    {
                        Procedure procedure = null;
                        if (objectResolver != null)
                        {
                            try
                            {
                                procedure = (Procedure)objectResolver.Resolve(type);
                            }
                            catch
                            {
                                try
                                {
                                    procedure = (Procedure)Activator.CreateInstance(type);
                                }
                                catch (Exception ex)
                                {
                                    BaseLog.LogWarning($"[ProcedureManager] Could not instantiate procedure '{type.Name}'. If it uses constructor injection, register it in RootLifetimeScope: {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            try
                            {
                                procedure = (Procedure)Activator.CreateInstance(type);
                            }
                            catch (Exception ex)
                            {
                                BaseLog.LogWarning($"[ProcedureManager] Could not instantiate procedure '{type.Name}': {ex.Message}");
                            }
                        }

                        if (procedure != null)
                        {
                            RegisterProcedure(procedure);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                BaseLog.LogWarning($"[ProcedureManager] Could not inspect assembly '{name}' while registering procedures: {exception.Message}");
            }
        }
    }
}
