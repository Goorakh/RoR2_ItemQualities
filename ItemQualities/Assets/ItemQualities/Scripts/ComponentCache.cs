using HG;
using RoR2;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ItemQualities
{
    internal static class ComponentCache
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetComponent<T>(GameObject gameObject, out T component) where T : Component
        {
            return TypedCache<T>.TryGetValue(gameObject, out component);
        }

        public static void Add<T>(GameObject gameObject, T component) where T : Component
        {
            if (ReferenceEquals(gameObject, null))
            {
                Log.Error($"({typeof(T).FullName}) {nameof(gameObject)} is null. {new StackTrace()}");
                return;
            }

            if (!TypedCache<T>.TryAdd(gameObject, component))
            {
                Log.Error($"({typeof(T).FullName}) Duplicate component registered to object {Util.GetGameObjectHierarchyName(gameObject)}. {new StackTrace()}");
            }
        }

        public static void Remove<T>(GameObject gameObject, T component) where T : Component
        {
            if (ReferenceEquals(gameObject, null))
            {
                Log.Error($"({typeof(T).FullName}) {nameof(gameObject)} is null. {new StackTrace()}");
                return;
            }

            if (TypedCache<T>.TryGetValue(gameObject, out T cachedComponent))
            {
                if (cachedComponent == component)
                {
                    TypedCache<T>.Remove(gameObject);
                }
                else
                {
                    Log.Error($"({typeof(T).FullName}) Attempting to remove non-cached component registered to object {Util.GetGameObjectHierarchyName(gameObject)}. {new StackTrace()}");
                }
            }
        }

        static class TypedCache<T> where T : Component
        {
            static readonly Dictionary<UnityObjectWrapperKey<GameObject>, T> _componentLookup = new Dictionary<UnityObjectWrapperKey<GameObject>, T>();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool TryGetValue(GameObject gameObject, out T component)
            {
                return _componentLookup.TryGetValue(gameObject, out component);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool TryAdd(GameObject gameObject, T component)
            {
                return _componentLookup.TryAdd(gameObject, component);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Remove(GameObject gameObject)
            {
                return _componentLookup.Remove(gameObject);
            }
        }
    }
}
