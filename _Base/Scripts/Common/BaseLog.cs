using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Base
{
    /// <summary>
    /// Conditional logging helper for framework internals.
    /// Define ENABLE_BASE_LOG to enable informational logs.
    /// Errors and exceptions are always emitted.
    /// </summary>
    public static class BaseLog
    {
        [Conditional("ENABLE_BASE_LOG")]
        public static void Log(string message)
        {
            Debug.Log(message);
        }

        [Conditional("ENABLE_BASE_LOG")]
        public static void LogWarning(string message)
        {
            Debug.LogWarning(message);
        }

        public static void LogError(string message)
        {
            Debug.LogError(message);
        }

        public static void LogException(System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}
