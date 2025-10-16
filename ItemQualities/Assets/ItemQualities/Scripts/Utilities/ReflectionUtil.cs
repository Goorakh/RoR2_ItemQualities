using System;
using System.Linq;
using System.Reflection;

namespace ItemQualities.Utilities
{
    public static class ReflectionUtil
    {
        public static MethodInfo FindImplicitConverter<TFrom, TTo>()
        {
            return FindImplicitConverter(typeof(TFrom), typeof(TTo));
        }

        public static MethodInfo FindImplicitConverter(Type from, Type to)
        {
            return findConverterMethod(from, to, "op_Implicit");
        }

        public static MethodInfo FindExplicitConverter<TFrom, TTo>()
        {
            return FindExplicitConverter(typeof(TFrom), typeof(TTo));
        }

        public static MethodInfo FindExplicitConverter(Type from, Type to)
        {
            return findConverterMethod(from, to, "op_Explicit");
        }

        static MethodInfo findConverterMethod(Type from, Type to, string name)
        {
            if (from is null)
                throw new ArgumentNullException(nameof(from));

            if (to is null)
                throw new ArgumentNullException(nameof(to));

            const BindingFlags ConverterMethodFlags = BindingFlags.Static | BindingFlags.Public;

            foreach (MethodInfo converterMethod in from.GetMethods(ConverterMethodFlags)
                                                       .Concat(to.GetMethods(ConverterMethodFlags))
                                                       .Where(m => m.IsSpecialName && m.Name == name))
            {
                if (converterMethod.ReturnType != to)
                    continue;

                ParameterInfo[] parameters = converterMethod.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != from)
                    continue;

                return converterMethod;
            }

            return null;
        }
    }
}
