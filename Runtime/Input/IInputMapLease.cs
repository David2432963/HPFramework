using System;

namespace HP.Framework.Input
{
    public interface IInputMapLease : IDisposable
    {
        string MapName { get; }
        bool IsValid { get; }
    }
}
