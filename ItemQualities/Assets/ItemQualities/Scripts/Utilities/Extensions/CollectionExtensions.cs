using RoR2.ContentManagement;
using System;
using System.Collections;
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

        public static T GetAndRemoveAt<T>(this IList<T> list, int index)
        {
            T value = list[index];
            list.RemoveAt(index);
            return value;
        }

        public static object GetAndRemoveAt(this IList list, int index)
        {
            object value = list[index];
            list.RemoveAt(index);
            return value;
        }

        public static void Add<T>(this NamedAssetCollection<T> namedAssetCollection, T value)
        {
            namedAssetCollection.Add(new T[] { value });
        }

        public static T GetSafe<T>(this IList<T> list, int index, T defaultValue = default)
        {
            if (list is null)
                throw new ArgumentNullException(nameof(list));

            return (uint)index < list.Count ? list[index] : defaultValue;
        }

        public static int IndexOf<T>(this IList<T> list, T item, IEqualityComparer<T> equalityComparer)
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

        public static int LastIndexOf<T>(this IList<T> list, T item, IEqualityComparer<T> equalityComparer)
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
