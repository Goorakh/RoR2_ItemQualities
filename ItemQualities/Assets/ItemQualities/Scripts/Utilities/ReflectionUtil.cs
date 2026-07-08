using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ItemQualities.Utilities
{
    public static class ReflectionUtil
    {
        private static class StaticCache<TArg1, TArg2>
        {
            private static MethodInfo _implicitConverter;
            private static bool _hasSearchedImplicitConverter;
            public static MethodInfo ImplicitConverter
            {
                get
                {
                    if (!_hasSearchedImplicitConverter)
                    {
                        _hasSearchedImplicitConverter = true;
                        _implicitConverter = FindImplicitConverter(typeof(TArg1), typeof(TArg2));
                        if (_implicitConverter == null)
                        {
                            Log.Error($"Failed to find implicit converter method from {typeof(TArg1).FullName} to {typeof(TArg2).FullName}");
                        }
                    }

                    return _implicitConverter;
                }
            }

            private static MethodInfo _explicitConverter;
            private static bool _hasSearchedExplicitConverter;
            public static MethodInfo ExplicitConverter
            {
                get
                {
                    if (!_hasSearchedExplicitConverter)
                    {
                        _hasSearchedExplicitConverter = true;
                        _explicitConverter = FindExplicitConverter(typeof(TArg1), typeof(TArg2));
                        if (_explicitConverter == null)
                        {
                            Log.Error($"Failed to find explicit converter method from {typeof(TArg1).FullName} to {typeof(TArg2).FullName}");
                        }
                    }

                    return _explicitConverter;
                }
            }

            private static MethodInfo _equalityOperator;
            private static bool _hasSearchedEqualityOperator;
            public static MethodInfo EqualityOperator
            {
                get
                {
                    if (!_hasSearchedEqualityOperator)
                    {
                        _hasSearchedEqualityOperator = true;
                        _equalityOperator = FindEqualityOperator(typeof(TArg1), typeof(TArg2));
                        if (_equalityOperator == null)
                        {
                            Log.Error($"Failed to find equalityi operator method for {typeof(TArg1).FullName} and {typeof(TArg2).FullName}");
                        }
                    }

                    return _equalityOperator;
                }
            }
        }

        public const BindingFlags AllFlags = (BindingFlags)(-1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo FindImplicitConverter<TFrom, TTo>()
        {
            return StaticCache<TFrom, TTo>.ImplicitConverter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo FindImplicitConverter(Type from, Type to)
        {
            return findConverterMethod(from, to, "op_Implicit");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo FindExplicitConverter<TFrom, TTo>()
        {
            return StaticCache<TFrom, TTo>.ExplicitConverter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo FindExplicitConverter(Type from, Type to)
        {
            return findConverterMethod(from, to, "op_Explicit");
        }

        private static MethodInfo findConverterMethod(Type from, Type to, string name)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo FindEqualityOperator<T>()
        {
            return FindEqualityOperator<T, T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MethodInfo FindEqualityOperator<T1, T2>()
        {
            return StaticCache<T1, T2>.EqualityOperator;
        }

        public static MethodInfo FindEqualityOperator(Type typeA, Type typeB)
        {
            const BindingFlags OperatorMethodFlags = BindingFlags.Static | BindingFlags.Public;

            foreach (MethodInfo converterMethod in typeA.GetMethods(OperatorMethodFlags)
                                                        .Concat(typeB.GetMethods(OperatorMethodFlags))
                                                        .Where(m => m.IsSpecialName && m.Name == "op_Equality"))
            {
                if (converterMethod.ReturnType != typeof(bool))
                    continue;

                ParameterInfo[] parameters = converterMethod.GetParameters();
                if (parameters.Length != 2)
                    continue;

                bool parametersMatch = (parameters[0].ParameterType == typeA && parameters[0].ParameterType == typeB) ||
                                       (parameters[1].ParameterType == typeA && parameters[1].ParameterType == typeB);

                if (!parametersMatch)
                    continue;

                return converterMethod;
            }

            return null;
        }
    }
}
