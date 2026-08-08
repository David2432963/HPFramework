namespace HP.Framework.Bootstrap
{
    /// <summary>
    /// Preferred semantic base for root-level application flow states such as Boot, MainMenu,
    /// Loading, and GameplaySession. Scene gameplay systems should live in scene/feature scopes and
    /// use VContainer entry points instead of becoming Procedures.
    /// </summary>
    public abstract class ApplicationProcedure : Procedure
    {
    }


}


