using RoR2.ContentManagement;
using System;
using System.Collections.Generic;

namespace ItemQualities.Utilities.Extensions
{
    public static class CollectionExtensions
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
    }
}
