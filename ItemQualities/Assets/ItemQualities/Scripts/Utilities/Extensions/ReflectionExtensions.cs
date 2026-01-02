using System.Reflection;
using UnityEngine;

namespace ItemQualities.Utilities.Extensions
{
    internal static class ReflectionExtensions
    {
        public static bool IsSerialized(this FieldInfo field)
        {
            return !field.IsNotSerialized && (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null);
        }
    }
}
