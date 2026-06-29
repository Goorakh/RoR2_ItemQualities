using RoR2.ContentManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ItemQualities.Utilities.Extensions
{
    internal static class CollectionExtensions
    {
        static class SharedSingleElementArray<T>
        {
            public static readonly T[] Array = new T[1];
        }

        public static void Add<T>(this NamedAssetCollection<T> namedAssetCollection, T value)
        {
            // OPTIMIZATION: Use shared array for passing info into collection to avoid allocations.
            // This is reliant on the fact that .Add() does not store a reference to the array and simply copies elements from it.
            ref readonly T[] array = ref SharedSingleElementArray<T>.Array;
            array[0] = value;
            try
            {
                namedAssetCollection.Add(array);
            }
            finally
            {
                array[0] = default(T);
            }
        }

        public static bool TryGetAsset<T>(this NamedAssetCollection<T> assetCollection, string name, out T asset)
            where T : class
        {
            asset = assetCollection.Find(name);
            return asset != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetSafe<T, TList>(this TList list, int index, in T defaultValue = default)
            where TList : IList<T>
        {
            return list != null && (uint)index < list.Count ? list[index] : defaultValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetSafe<T, TList>(this TList list, int index, out T value)
            where TList : IList<T>
        {
            if (list != null && (uint)index < list.Count)
            {
                value = list[index];
                return true;
            }

            value = default;
            return false;
        }

        public static int IndexOf<T, TList, TComparer>(this TList list, T item, TComparer equalityComparer)
            where TList : IList<T>
            where TComparer : IEqualityComparer<T>
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

        public static int LastIndexOf<T, TList, TComparer>(this TList list, T item, TComparer equalityComparer)
            where TList : IList<T>
            where TComparer : IEqualityComparer<T>
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

        /// <summary>
        /// Calls <see cref="IEnumerator.MoveNext"/> on <typeparamref name="TEnumerator"/>, safely swallowing any exceptions.
        /// </summary>
        /// <typeparam name="TEnumerator"></typeparam>
        /// <param name="enumerator"></param>
        /// <returns>
        /// <see langword="true"/> if the enumerator has more elements.
        /// <br/>
        /// <see langword="false"/> if the enumerator has no more elements, or an exception occured within <see cref="IEnumerator.MoveNext"/>
        /// </returns>
        public static bool SafeMoveNext<TEnumerator>(this TEnumerator enumerator)
            where TEnumerator : IEnumerator
        {
            try
            {
                if (enumerator.MoveNext())
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e.ToString());
            }

            return false;
        }
    }
}
