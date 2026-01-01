using System;
using UnityEngine;

namespace ItemQualities.Utilities.Extensions
{
    internal static class UnityExtensions
    {
        public static bool TryGetComponentCached<T>(this GameObject gameObject, out T component) where T : Component
        {
            if (!gameObject)
                throw new ArgumentNullException(nameof(gameObject));

            return ComponentCache.TryGetComponent(gameObject, out component);
        }

        public static T GetComponentCached<T>(this GameObject gameObject) where T : Component
        {
            if (!gameObject)
                throw new ArgumentNullException(nameof(gameObject));

            return ComponentCache.TryGetComponent(gameObject, out T component) ? component : null;
        }

        public static bool TryGetComponentCached<T>(this Component srcComponent, out T component) where T : Component
        {
            if (!srcComponent)
                throw new ArgumentNullException(nameof(srcComponent));

            return ComponentCache.TryGetComponent(srcComponent.gameObject, out component);
        }

        public static T GetComponentCached<T>(this Component srcComponent) where T : Component
        {
            if (!srcComponent)
                throw new ArgumentNullException(nameof(srcComponent));

            return ComponentCache.TryGetComponent(srcComponent.gameObject, out T component) ? component : null;
        }
    }
}
