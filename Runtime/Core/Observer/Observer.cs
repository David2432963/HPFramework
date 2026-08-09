namespace HP.Framework.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Lightweight generic event hub.
    /// Supports both parameterless callbacks (Action) and parameterized callbacks (Action<object>).
    /// Publish paths avoid enum-key boxing; subscriptions, SubscribeOnce closures and
    /// diagnostic listener-count queries may allocate.
    /// 
    /// Example Usage:
    /// <code>
    /// // 1. Parameterless Events
    /// // Subscribe:
    /// Observer.Subscribe(GameEventId.PlayerDie, OnPlayerDie);
    /// 
    /// // Publish:
    /// Observer.Publish(GameEventId.PlayerDie);
    /// 
    /// // Unsubscribe:
    /// Observer.Unsubscribe(GameEventId.PlayerDie, OnPlayerDie);
    /// </code>
    /// </summary>
    public static class Observer
    {
        private static readonly List<Action> clearCallbacks = new List<Action>();

        // A nested generic class that keeps separate type-safe dictionaries for each enum type T
        private static class Registry<T> where T : struct, Enum
        {
            public static readonly Dictionary<T, Action> listenersNoParam = new Dictionary<T, Action>();
            public static readonly Dictionary<T, Action<object>> listenersWithParam = new Dictionary<T, Action<object>>();

            static Registry()
            {
                lock (clearCallbacks)
                {
                    clearCallbacks.Add(ClearRegistry);
                }
            }

            private static void ClearRegistry()
            {
                listenersNoParam.Clear();
                listenersWithParam.Clear();
            }
        }

        private static class TypedRegistry<TEnum, TData> where TEnum : struct, Enum
        {
            public static readonly Dictionary<TEnum, Action<TData>> listeners = new Dictionary<TEnum, Action<TData>>();

            static TypedRegistry()
            {
                lock (clearCallbacks)
                {
                    clearCallbacks.Add(ClearRegistry);
                }
            }

            private static void ClearRegistry()
            {
                listeners.Clear();
            }
        }

        /// <summary>
        /// Register a listener for a parameterless event.
        /// </summary>
        public static void Subscribe<T>(T eventId, Action listener) where T : struct, Enum
        {
            if (listener == null)
            {
                return;
            }

            var dict = Registry<T>.listenersNoParam;
            if (dict.TryGetValue(eventId, out var existing))
            {
                dict[eventId] = existing + listener;
            }
            else
            {
                dict[eventId] = listener;
            }
        }

        /// <summary>
        /// Register a listener for a parameterless event that removes itself after the first notification.
        /// </summary>
        public static void SubscribeOnce<T>(T eventId, Action listener) where T : struct, Enum
        {
            if (listener == null)
            {
                return;
            }

            Action wrapper = null;
            wrapper = () =>
            {
                Unsubscribe(eventId, wrapper);
                listener.Invoke();
            };

            Subscribe(eventId, wrapper);
        }

        /// <summary>
        /// Register a listener for an event with an object parameter.
        /// </summary>
        public static void Subscribe<T>(T eventId, Action<object> listener) where T : struct, Enum
        {
            if (listener == null)
            {
                return;
            }

            var dict = Registry<T>.listenersWithParam;
            if (dict.TryGetValue(eventId, out var existing))
            {
                dict[eventId] = existing + listener;
            }
            else
            {
                dict[eventId] = listener;
            }
        }

        /// <summary>
        /// Register a listener for an event with an object parameter that removes itself after the first notification.
        /// </summary>
        public static void SubscribeOnce<T>(T eventId, Action<object> listener) where T : struct, Enum
        {
            if (listener == null)
            {
                return;
            }

            Action<object> wrapper = null;
            wrapper = param =>
            {
                Unsubscribe(eventId, wrapper);
                listener.Invoke(param);
            };

            Subscribe(eventId, wrapper);
        }

        /// <summary>
        /// Remove a parameterless listener for the given event.
        /// </summary>
        public static void Unsubscribe<T>(T eventId, Action listener) where T : struct, Enum
        {
            if (listener == null)
            {
                return;
            }

            var dict = Registry<T>.listenersNoParam;
            if (dict.TryGetValue(eventId, out var existing))
            {
                Action remaining = existing - listener;
                if (remaining == null)
                {
                    dict.Remove(eventId);
                }
                else
                {
                    dict[eventId] = remaining;
                }
            }
        }

        /// <summary>
        /// Remove a parameterized listener for the given event.
        /// </summary>
        public static void Unsubscribe<T>(T eventId, Action<object> listener) where T : struct, Enum
        {
            if (listener == null)
            {
                return;
            }

            var dict = Registry<T>.listenersWithParam;
            if (dict.TryGetValue(eventId, out var existing))
            {
                Action<object> remaining = existing - listener;
                if (remaining == null)
                {
                    dict.Remove(eventId);
                }
                else
                {
                    dict[eventId] = remaining;
                }
            }
        }

        /// <summary>
        /// Remove every listener for the given event.
        /// </summary>
        public static bool Clear<T>(T eventId) where T : struct, Enum
        {
            bool removedNoParam = Registry<T>.listenersNoParam.Remove(eventId);
            bool removedWithParam = Registry<T>.listenersWithParam.Remove(eventId);
            return removedNoParam || removedWithParam;
        }

        /// <summary>
        /// Remove every registered listener.
        /// </summary>
        public static void Clear()
        {
            lock (clearCallbacks)
            {
                foreach (var callback in clearCallbacks)
                {
                    callback?.Invoke();
                }
            }
        }

        /// <summary>
        /// Register a typed listener for a parameterized event without boxing value types.
        /// </summary>
        public static void Subscribe<TEnum, TData>(TEnum eventId, Action<TData> listener) where TEnum : struct, Enum
        {
            if (listener == null)
            {
                return;
            }

            var dict = TypedRegistry<TEnum, TData>.listeners;
            if (dict.TryGetValue(eventId, out var existing))
            {
                dict[eventId] = existing + listener;
            }
            else
            {
                dict[eventId] = listener;
            }
        }

        /// <summary>
        /// Remove a typed listener for the given event.
        /// </summary>
        public static void Unsubscribe<TEnum, TData>(TEnum eventId, Action<TData> listener) where TEnum : struct, Enum
        {
            if (listener == null)
            {
                return;
            }

            var dict = TypedRegistry<TEnum, TData>.listeners;
            if (dict.TryGetValue(eventId, out var existing))
            {
                Action<TData> remaining = existing - listener;
                if (remaining == null)
                {
                    dict.Remove(eventId);
                }
                else
                {
                    dict[eventId] = remaining;
                }
            }
        }

        /// <summary>
        /// Broadcast a typed event without boxing for typed listeners. If an Action<object>
        /// listener is also registered, a value-type payload can still be boxed for that legacy path.
        /// </summary>
        public static void Publish<TEnum, TData>(TEnum eventId, TData param) where TEnum : struct, Enum
        {
            if (Registry<TEnum>.listenersNoParam.TryGetValue(eventId, out var action))
            {
                action?.Invoke();
            }

            if (Registry<TEnum>.listenersWithParam.TryGetValue(eventId, out var actionWithParam))
            {
                actionWithParam?.Invoke(param);
            }

            if (TypedRegistry<TEnum, TData>.listeners.TryGetValue(eventId, out var actionTyped))
            {
                actionTyped?.Invoke(param);
            }
        }

        /// <summary>
        /// Broadcast an event to all matching parameterless and parameterized listeners.
        /// </summary>
        public static void Publish<T>(T eventId, object param = null) where T : struct, Enum
        {
            if (Registry<T>.listenersNoParam.TryGetValue(eventId, out var action))
            {
                action?.Invoke();
            }

            if (Registry<T>.listenersWithParam.TryGetValue(eventId, out var actionWithParam))
            {
                actionWithParam?.Invoke(param);
            }
        }

        /// <summary>
        /// Check whether any listener is currently registered for the given event.
        /// </summary>
        public static bool HasListeners<T>(T eventId) where T : struct, Enum
        {
            return GetListenerCount(eventId) > 0;
        }

        /// <summary>
        /// Get the number of listeners registered for the given event.
        /// </summary>
        public static int GetListenerCount<T>(T eventId) where T : struct, Enum
        {
            int count = 0;
            if (Registry<T>.listenersNoParam.TryGetValue(eventId, out var action) && action != null)
            {
                count += action.GetInvocationList().Length;
            }

            if (Registry<T>.listenersWithParam.TryGetValue(eventId, out var actionWithParam) && actionWithParam != null)
            {
                count += actionWithParam.GetInvocationList().Length;
            }

            return count;
        }
    }


}


