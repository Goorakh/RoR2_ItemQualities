using EntityStates;
using System;
using UnityEngine;

namespace ItemQualities.Utilities.Extensions
{
    static class EntityStateExtensions
    {
        public static bool TryGetComponent(this EntityState entityState, Type componentType, out Component component)
        {
            if (!entityState?.outer)
            {
                component = null;
                return false;
            }

            return entityState.outer.TryGetComponent(componentType, out component);
        }

        internal static bool TryGetComponentCached<T>(this EntityState entityState, out T component) where T : Component
        {
            if (!entityState?.outer)
            {
                component = null;
                return false;
            }

            return entityState.outer.TryGetComponentCached(out component);
        }

        internal static T GetComponentCached<T>(this EntityState entityState) where T : Component
        {
            if (!entityState?.outer)
                return null;

            return entityState.outer.GetComponentCached<T>();
        }
    }
}
