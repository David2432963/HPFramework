using System;
using System.Collections.Generic;

namespace VContainer.Internal
{
    class FreeList<T> where T : class
    {
        public bool IsDisposed => lastIndex == -2;
        public int Length => lastIndex + 1;

        readonly object gate = new object();
        T[] values;
        int lastIndex = -1;

        public FreeList(int initialCapacity)
        {
            values = new T[Math.Max(1, initialCapacity)];
        }

#if NETSTANDARD2_1
        public ReadOnlySpan<T> AsSpan()
        {
            if (lastIndex < 0)
            {
                return ReadOnlySpan<T>.Empty;
            }

            return values.AsSpan(0, lastIndex + 1);
        }
#endif

        public T this[int index] => values[index];

        public void Add(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            lock (gate)
            {
                CheckDispose();

                var index = FindNullIndex(values);
                if (index == -1)
                {
                    var length = values.Length;
                    var newLength = Math.Max(length + 1, length + length / 2);
                    var newValues = new T[newLength];
                    Array.Copy(values, newValues, length);
                    values = newValues;
                    index = length;
                }

                values[index] = item;
                if (lastIndex < index)
                {
                    lastIndex = index;
                }
            }
        }

        public void RemoveAt(int index)
        {
            lock (gate)
            {
                CheckDispose();
                if (index < 0 || index >= values.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                ref var value = ref values[index];
                if (value == null)
                {
                    throw new KeyNotFoundException($"key index {index} is not found.");
                }

                value = null;
                if (index == lastIndex)
                {
                    lastIndex = FindLastNonNullIndex(values, index - 1);
                }
            }
        }

        public bool Remove(T value)
        {
            if (value == null) return false;

            lock (gate)
            {
                CheckDispose();
                if (lastIndex < 0) return false;

                for (var i = 0; i <= lastIndex; i++)
                {
                    if (!ReferenceEquals(values[i], value)) continue;

                    values[i] = null;
                    if (i == lastIndex)
                    {
                        lastIndex = FindLastNonNullIndex(values, i - 1);
                    }
                    return true;
                }
            }

            return false;
        }

        public void Clear()
        {
            lock (gate)
            {
                CheckDispose();
                if (lastIndex >= 0)
                {
                    Array.Clear(values, 0, lastIndex + 1);
                    lastIndex = -1;
                }
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (IsDisposed) return;
                Array.Clear(values, 0, values.Length);
                lastIndex = -2;
            }
        }

        void CheckDispose()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        static int FindNullIndex(T[] target)
        {
            for (var i = 0; i < target.Length; i++)
            {
                if (target[i] == null) return i;
            }

            return -1;
        }

        static int FindLastNonNullIndex(T[] target, int lastIndex)
        {
            for (var i = Math.Min(lastIndex, target.Length - 1); i >= 0; i--)
            {
                if (target[i] != null) return i;
            }

            return -1;
        }
    }
}
