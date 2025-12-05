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
    }
}
