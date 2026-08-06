using System.Diagnostics;
using UnityEngine;

namespace Base
{
    /// <summary>
    /// Zero-allocation conditional logging helper for _Base framework internal logs.
    /// Define 'ENABLE_BASE_LOG' in Player Settings -> Scripting Define Symbols to enable detailed logging.
    /// LogError and LogException are always compiled.
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
