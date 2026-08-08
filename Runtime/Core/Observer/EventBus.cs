using System;
using System.Collections.Generic;

namespace HP.Framework.Common
{
    /// <summary>
    /// Scope-friendly typed event bus. Prefer this over the static Observer for new code.
    /// Dispose is normally owned by the VContainer scope that registered the bus.
    /// </summary>
    public interface IEventBus : IDisposable
    {
        IDisposable Subscribe<TEvent>(Action<TEvent> listener);
        IDisposable SubscribeOnce<TEvent>(Action<TEvent> listener);
        void Unsubscribe<TEvent>(Action<TEvent> listener);
        void Publish<TEvent>(TEvent eventData);
        void Clear();
    }

    public sealed class EventBus : IEventBus
    {
        private sealed class Subscription<TEvent> : IDisposable
        {
            private EventBus owner;
            private Action<TEvent> listener;

            public Subscription(EventBus owner, Action<TEvent> listener)
            {
                this.owner = owner;
                this.listener = listener;
            }

            public void Dispose()
            {
                EventBus currentOwner = owner;
                Action<TEvent> currentListener = listener;
                owner = null;
                listener = null;
                currentOwner?.Unsubscribe(currentListener);
            }
        }

        private readonly Dictionary<Type, Delegate> listeners = new Dictionary<Type, Delegate>();
        private bool disposed;

        public IDisposable Subscribe<TEvent>(Action<TEvent> listener)
        {
            ThrowIfDisposed();
            if (listener == null)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            Type eventType = typeof(TEvent);
            listeners.TryGetValue(eventType, out Delegate existing);
            listeners[eventType] = (Action<TEvent>)existing + listener;
            return new Subscription<TEvent>(this, listener);
        }

        public IDisposable SubscribeOnce<TEvent>(Action<TEvent> listener)
        {
            ThrowIfDisposed();
            if (listener == null)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            Action<TEvent> wrapper = null;
            wrapper = eventData =>
            {
                Unsubscribe(wrapper);
                listener(eventData);
            };
            return Subscribe(wrapper);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> listener)
        {
            if (disposed || listener == null)
            {
                return;
            }

            Type eventType = typeof(TEvent);
            if (!listeners.TryGetValue(eventType, out Delegate existing))
            {
                return;
            }

            Action<TEvent> remaining = (Action<TEvent>)existing - listener;
            if (remaining == null)
            {
                listeners.Remove(eventType);
            }
            else
            {
                listeners[eventType] = remaining;
            }
        }

        public void Publish<TEvent>(TEvent eventData)
        {
            ThrowIfDisposed();
            if (listeners.TryGetValue(typeof(TEvent), out Delegate existing))
            {
                ((Action<TEvent>)existing)?.Invoke(eventData);
            }
        }

        public void Clear()
        {
            if (!disposed)
            {
                listeners.Clear();
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            listeners.Clear();
            disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(EventBus));
            }
        }
    }
}
