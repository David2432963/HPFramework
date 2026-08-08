namespace HP.Framework.Common
{
    using System.Collections.Generic;

    /// <summary>
    /// Collection helpers that keep small stack/list operations readable.
    /// </summary>
    public static class CollectionExtensions
    {
        /// <summary>
        /// Check whether a collection has no items.
        /// </summary>
        public static bool IsNullOrEmpty<T>(this ICollection<T> collection)
        {
            return collection == null || collection.Count == 0;
        }

        /// <summary>
        /// Remove and return the last item from a list when possible.
        /// </summary>
        public static bool TryPop<T>(this List<T> list, out T value)
        {
            if (list == null || list.Count == 0)
            {
                value = default;
                return false;
            }

            var lastIndex = list.Count - 1;
            value = list[lastIndex];
            list.RemoveAt(lastIndex);
            return true;
        }

        /// <summary>
        /// Read the last item without removing it.
        /// </summary>
        public static bool TryPeek<T>(this IList<T> list, out T value)
        {
            if (list == null || list.Count == 0)
            {
                value = default;
                return false;
            }

            value = list[list.Count - 1];
            return true;
        }
    }


}


