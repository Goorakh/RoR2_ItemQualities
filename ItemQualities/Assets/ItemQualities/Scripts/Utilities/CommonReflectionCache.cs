using System;
using System.Reflection;
using UnityEngine;

namespace ItemQualities.Utilities
{
    internal static class CommonReflectionCache
    {
        public static class AddComponent
        {
            public static readonly MethodInfo Method;

            static AddComponent()
            {
                Method = typeof(GameObject).GetMethod(nameof(GameObject.AddComponent), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, Array.Empty<Type>(), Array.Empty<ParameterModifier>());
                if (Method == null || !Method.IsGenericMethodDefinition)
                {
                    Log.Error("Failed to find method GameObject.AddComponent<T>()");
                    Method = null;
                }
            }

            public static class OfType<T>
                where T : Component
            {
                public static readonly MethodInfo Method = AddComponent.Method?.MakeGenericMethod(typeof(T));
            }
        }
    }
}
