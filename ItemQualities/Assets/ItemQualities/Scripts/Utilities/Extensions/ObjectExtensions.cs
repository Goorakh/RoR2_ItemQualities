using System;
using System.Collections.Generic;
using System.Reflection;

namespace ItemQualities.Utilities.Extensions
{
    public static class ObjectExtensions
    {
        static readonly Dictionary<Type, ConstructorInfo> _cloneConstructorCache = new Dictionary<Type, ConstructorInfo>();

        public static T ShallowCopy<T>(this T source, BindingFlags fieldBindingFlags = BindingFlags.Instance | BindingFlags.Public)
        {
            if (source is null)
                return default;

            if (source is ValueType)
                return source;

            if (source is ICloneable cloneable)
                return (T)cloneable.Clone();

            Type type = source.GetType();
            if (TryFindCloneConstructor(type, out ConstructorInfo cloneConstructor))
                return (T)cloneConstructor.Invoke(new object[] { source });

            T copyInstance = (T)Activator.CreateInstance(type);
            source.ShallowCopy(ref copyInstance, fieldBindingFlags);
            return copyInstance;
        }

        public static void ShallowCopy<T>(this T source, ref T dest, BindingFlags fieldBindingFlags = BindingFlags.Instance | BindingFlags.Public)
        {
            if (source is null)
            {
                dest = default;
                return;
            }

            if (source is ValueType)
            {
                dest = source;
                return;
            }

            if (source is ICloneable cloneable)
            {
                dest = (T)cloneable.Clone();
                return;
            }

            foreach (FieldInfo field in source.GetType().GetFields(fieldBindingFlags))
            {
                try
                {
                    Type fieldType = field.FieldType;
                    object fieldValue = field.GetValue(source);
                    
                    if (fieldValue is ICloneable fieldValueCloneable)
                    {
                        fieldValue = fieldValueCloneable.Clone();
                    }
                    else if (fieldType.IsClass && fieldValue != null)
                    {
                        if (fieldValue is UnityEngine.Object unityObject)
                        {
                            // TODO: Search for child/component reference in dest, they shouldn't be instantiated anyway, so safe to ignore for now.
                        }
                        else if (TryFindCloneConstructor(fieldType, out ConstructorInfo cloneConstructor))
                        {
                            try
                            {
                                fieldValue = cloneConstructor.Invoke(new object[] { fieldValue });
                            }
                            catch (Exception ex)
                            {
                                Log.Error_NoCallerPrefix($"Failed to copy {field.DeclaringType.FullName}.{field.Name} ({fieldType.FullName}) via constructor, same object instance will be used in the cloned object: {ex}");
                            }
                        }
                        else
                        {
                            Log.Warning($"Failed to find copy method for reference type field {field.DeclaringType.FullName}.{field.Name} ({fieldType.FullName}), same object instance will be used in the cloned object.");
                        }
                    }

                    field.SetValue(dest, fieldValue);
                }
                catch (Exception ex)
                {
                    Log.Warning_NoCallerPrefix($"Failed to set copy field value {field.DeclaringType.FullName}.{field.Name} ({field.FieldType.FullName}): {ex}");
                }
            }
        }

        public static bool TryFindCloneConstructor(Type type, out ConstructorInfo cloneConstructor)
        {
            if (_cloneConstructorCache.TryGetValue(type, out cloneConstructor))
                return true;

            ConstructorInfo shallowestCloneConstructor = null;
            Type bestCloneConstructorParameterType = null;

            foreach (ConstructorInfo constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                ParameterInfo[] parameters = constructor.GetParameters();
                if (parameters.Length != 1)
                    continue;

                Type parameterType = parameters[0].ParameterType;

                if (parameterType.IsAssignableFrom(type))
                {
                    if (bestCloneConstructorParameterType == null ||
                        (parameterType != bestCloneConstructorParameterType && bestCloneConstructorParameterType.IsAssignableFrom(parameterType)))
                    {
                        shallowestCloneConstructor = constructor;
                        bestCloneConstructorParameterType = parameterType;
                    }
                }
            }

            cloneConstructor = shallowestCloneConstructor;

            if (cloneConstructor == null)
                return false;

            _cloneConstructorCache[type] = cloneConstructor;
            return true;
        }
    }
}
