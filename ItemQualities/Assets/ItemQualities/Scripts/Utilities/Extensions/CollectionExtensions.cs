using RoR2.ContentManagement;
using System;
using System.Collections.Generic;

namespace ItemQualities.Utilities.Extensions
{
    internal static class CollectionExtensions
    {
        public static void EnsureCapacity<T>(this List<T> list, int capacity)
        {
            if (list is null)
                throw new ArgumentNullException(nameof(list));

            if (list.Capacity < capacity)
            {
                list.Capacity = capacity;
            }
        }

        public static void Add<T>(this NamedAssetCollection<T> namedAssetCollection, T value)
        {
            namedAssetCollection.Add(new T[] { value });
        }

        public static T GetSafe<T, TList>(this TList list, int index, in T defaultValue = default)
            where TList : IList<T>
        {
            if (list is null)
                throw new ArgumentNullException(nameof(list));

            return (uint)index < list.Count ? list[index] : defaultValue;
        }

        public static int IndexOf<T, TList>(this IList<T> list, T item, IEqualityComparer<T> equalityComparer)
            where TList : IList<T>
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (equalityComparer.Equals(list[i], item))
                {
                    return i;
                }
            }

            return -1;
        }

        public static int LastIndexOf<T, TList>(this IList<T> list, T item, IEqualityComparer<T> equalityComparer)
            where TList : IList<T>
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (equalityComparer.Equals(list[i], item))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
