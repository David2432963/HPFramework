using UnityEngine;
using Base;

/// <summary>
/// Base class for full-screen UI pages.
/// </summary>
public class BaseScreen : MonoBehaviour
{
    /// <summary>
    /// Unity lifecycle method. Override this in subclasses ONLY for:
    /// 1. Caching internal components (GetComponent).
    /// 2. Binding local UI component listeners (button.onClick.AddListener).
    /// DO NOT depend on external services or passed data in Awake.
    /// </summary>
    protected virtual void Awake()
    {
    }

    /// <summary>
    /// Domain initialization method called explicitly by UIManager/Controller after instantiation.
    /// Override this in subclasses for:
    /// 1. Dependency Injection (receiving services, profile data, controllers).
    /// 2. Top-down initialization of child sub-panels / tabs.
    /// Called ONCE when the screen is created.
    /// </summary>
    public virtual void Initialize()
    {
        BaseLog.Log($"[UI] Initialize Screen: {gameObject.name}");
    }

    /// <summary>
    /// Make the Screen visible and call OnShow.
    /// </summary>
    public virtual void Show()
    {
        BaseLog.Log($"[UI] Show Screen: {gameObject.name}");
        gameObject.SetActive(true);
        OnShow();
    }

    /// <summary>
    /// Hide the Screen and call OnHide.
    /// </summary>
    public virtual void Hide()
    {
        BaseLog.Log($"[UI] Hide Screen: {gameObject.name}");
        OnHide();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Check whether the screen is currently active.
    /// </summary>
    public bool IsVisible => gameObject.activeSelf;

    /// <summary>
    /// Override this to run logic after Show().
    /// </summary>
    protected virtual void OnShow()
    {
    }

    /// <summary>
    /// Override this to run logic before Hide().
    /// </summary>
    protected virtual void OnHide()
    {
    }

    /// <summary>
    /// Custom destroy method called by UIManager.
    /// </summary>
    public virtual void Destroy()
    {
        BaseLog.Log($"[UI] Destroy Screen: {gameObject.name}");
        UnityEngine.Object.Destroy(gameObject);
    }

    /// <summary>
    /// Called before the screen is destroyed.
    /// </summary>
    protected virtual void OnDestroy()
    {
    }
}
