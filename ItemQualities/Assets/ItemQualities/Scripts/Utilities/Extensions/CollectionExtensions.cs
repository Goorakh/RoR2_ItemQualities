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
    }
}
