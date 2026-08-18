using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("HP.Framework.Bootstrap")]
[assembly: InternalsVisibleTo("HP.Framework.Tests.Runtime")]
[assembly: InternalsVisibleTo("HP.Framework.Tests.Runtime.Animations")]

namespace HP.Framework.Animations
{
    internal interface IFrameworkTickable
    {
        void Tick();
    }

    internal static class FrameworkTickRegistry
    {
        private const int InitialCapacity = 16;

        private static readonly List<IFrameworkTickable> ActiveItems =
            new List<IFrameworkTickable>(InitialCapacity);
        private static readonly HashSet<IFrameworkTickable> RegisteredItems =
            new HashSet<IFrameworkTickable>();
        private static readonly List<IFrameworkTickable> PendingAdds =
            new List<IFrameworkTickable>(InitialCapacity);
        private static readonly List<IFrameworkTickable> PendingRemovals =
            new List<IFrameworkTickable>(InitialCapacity);

        private static object activeDriver;
        private static bool isDispatching;
        private static bool clearRequested;

        internal static bool HasActiveDriver => activeDriver != null;
        internal static int ActiveCount => RegisteredItems.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            activeDriver = null;
            ClearItems();
        }

        internal static bool AttachDriver(object driver)
        {
            if (driver == null)
            {
                throw new ArgumentNullException(nameof(driver));
            }

            if (ReferenceEquals(activeDriver, driver))
            {
                return true;
            }

            if (activeDriver != null)
            {
                Debug.LogError(
                    "[HP Framework] Multiple framework tick drivers are active. " +
                    "Only the first RootLifetimeScope will dispatch runtime tickables.");
                return false;
            }

            activeDriver = driver;
            return true;
        }

        internal static void DetachDriver(object driver)
        {
            if (!ReferenceEquals(activeDriver, driver))
            {
                return;
            }

            activeDriver = null;
            if (isDispatching)
            {
                RegisteredItems.Clear();
                clearRequested = true;
                return;
            }

            ClearItems();
        }

        internal static bool Register(IFrameworkTickable item)
        {
            if (item == null || !RegisteredItems.Add(item))
            {
                return false;
            }

            if (isDispatching)
            {
                PendingAdds.Add(item);
            }
            else
            {
                ActiveItems.Add(item);
            }

            return true;
        }

        internal static bool Unregister(IFrameworkTickable item)
        {
            if (item == null || !RegisteredItems.Remove(item))
            {
                return false;
            }

            if (isDispatching)
            {
                PendingRemovals.Add(item);
            }
            else
            {
                ActiveItems.Remove(item);
            }

            return true;
        }

        internal static void Dispatch(object driver)
        {
            if (!ReferenceEquals(activeDriver, driver) || isDispatching)
            {
                return;
            }

            isDispatching = true;
            try
            {
                int count = ActiveItems.Count;
                for (int i = 0; i < count; i++)
                {
                    IFrameworkTickable item = ActiveItems[i];
                    if (!RegisteredItems.Contains(item))
                    {
                        continue;
                    }

                    if (item is UnityEngine.Object unityObject && unityObject == null)
                    {
                        Unregister(item);
                        continue;
                    }

                    try
                    {
                        item.Tick();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        Unregister(item);
                    }
                }
            }
            finally
            {
                isDispatching = false;
                ApplyPendingChanges();
            }
        }

        private static void ApplyPendingChanges()
        {
            if (clearRequested)
            {
                ClearItems();
                return;
            }

            for (int i = 0; i < PendingRemovals.Count; i++)
            {
                ActiveItems.Remove(PendingRemovals[i]);
            }

            for (int i = 0; i < PendingAdds.Count; i++)
            {
                IFrameworkTickable item = PendingAdds[i];
                if (RegisteredItems.Contains(item) && !ActiveItems.Contains(item))
                {
                    ActiveItems.Add(item);
                }
            }

            PendingRemovals.Clear();
            PendingAdds.Clear();
        }

        private static void ClearItems()
        {
            ActiveItems.Clear();
            RegisteredItems.Clear();
            PendingAdds.Clear();
            PendingRemovals.Clear();
            isDispatching = false;
            clearRequested = false;
        }
    }
}
