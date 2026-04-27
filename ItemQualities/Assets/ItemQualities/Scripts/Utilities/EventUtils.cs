using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ItemQualities.Utilities
{
    internal static class EventUtils
    {
        static readonly Dictionary<FieldInfo, Delegate> _invokeDelegateCache = new Dictionary<FieldInfo, Delegate>();

        public static TDelegate GetInvokeMethodDelegate<TDelegate>(Type eventDeclaringType, string eventName)
            where TDelegate : Delegate
        {
            if (eventDeclaringType is null)
                throw new ArgumentNullException(nameof(eventDeclaringType));

            if (string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException($"'{nameof(eventName)}' cannot be null or whitespace.", nameof(eventName));

            FieldInfo eventField = eventDeclaringType.GetField(eventName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
            if (eventField == null)
            {
                Log.Error($"Failed to find field: {eventDeclaringType.FullName}.{eventName}");
                return null;
            }

            return GetInvokeMethodDelegate<TDelegate>(eventField);
        }

        public static TDelegate GetInvokeMethodDelegate<TDelegate>(FieldInfo eventDelegateField)
            where TDelegate : Delegate
        {
            if (eventDelegateField is null)
                throw new ArgumentNullException(nameof(eventDelegateField));

            if (_invokeDelegateCache.TryGetValue(eventDelegateField, out Delegate cachedInvokeDelegate))
            {
                if (!typeof(TDelegate).IsAssignableFrom(cachedInvokeDelegate.GetType()))
                {
                    throw new ArgumentException($"Invoke delegate type {typeof(TDelegate).FullName} does not match invoke delegate {cachedInvokeDelegate.GetType().FullName}", nameof(TDelegate));
                }

                return (TDelegate)cachedInvokeDelegate;
            }

            Type delegateType = eventDelegateField.FieldType;

            MethodInfo delegateInvokeMethod = delegateType.GetMethod(nameof(Action.Invoke), BindingFlags.Public | BindingFlags.Instance);
            if (delegateInvokeMethod is null)
            {
                throw new ArgumentException($"Field '{eventDelegateField.DeclaringType.FullName + "." + eventDelegateField.Name}' is not a delegate type", nameof(eventDelegateField));
            }

            ParameterInfo[] delegateInvokeParameters = delegateInvokeMethod.GetParameters();
            Type[] delegateInvokeParameterTypes = Array.ConvertAll(delegateInvokeParameters, p => p.ParameterType);
            Type[] invokeParameterTypes;
            if (eventDelegateField.IsStatic)
            {
                invokeParameterTypes = delegateInvokeParameterTypes;
            }
            else
            {
                Type instanceParameterType = eventDelegateField.DeclaringType;
                if (instanceParameterType.IsValueType)
                {
                    instanceParameterType = instanceParameterType.MakeByRefType();
                }

                invokeParameterTypes = new Type[1 + delegateInvokeParameterTypes.Length];
                invokeParameterTypes[0] = instanceParameterType;
                Array.Copy(delegateInvokeParameterTypes, 0, invokeParameterTypes, 1, delegateInvokeParameterTypes.Length);
            }

            using DynamicMethodDefinition dmd = new DynamicMethodDefinition("DMD_Invoke<" + eventDelegateField.DeclaringType.FullName + "." + eventDelegateField.Name + ">", delegateInvokeMethod.ReturnType, invokeParameterTypes);
            using ILContext il = new ILContext(dmd.Definition);
            il.ReferenceBag = RuntimeILReferenceBag.Instance;
            il.Invoke(il =>
            {
                ILCursor c = new ILCursor(il);

                bool hasInstanceParameter = !eventDelegateField.IsStatic;

                if (hasInstanceParameter)
                {
                    c.Emit(OpCodes.Ldarg_0);
                    c.Emit(OpCodes.Ldfld, eventDelegateField);
                }
                else
                {
                    c.Emit(OpCodes.Ldsfld, eventDelegateField);
                }

                c.Emit(OpCodes.Dup);
                ILLabel eventFieldNotNullLabel = c.DefineLabel();
                c.Emit(OpCodes.Brtrue, eventFieldNotNullLabel);

                c.Emit(OpCodes.Pop);
                
                if (delegateInvokeMethod.ReturnType != typeof(void))
                {
                    c.Emit(OpCodes.Ldstr, $"Attempting to invoke delegate {eventDelegateField.DeclaringType.FullName}.{eventDelegateField.Name} ({delegateType.FullName}) without a delegate instance, the default value for the return type ({delegateInvokeMethod.ReturnType.FullName}) will be returned");
                    c.EmitDelegate<Action<string>>(Log.Warning_NoCallerPrefix);

                    c.EmitDefaultValue(delegateInvokeMethod.ReturnType);
                }

                c.Emit(OpCodes.Ret);

                c.MarkLabel(eventFieldNotNullLabel);

                for (int i = 0; i < delegateInvokeParameterTypes.Length; i++)
                {
                    c.Emit(OpCodes.Ldarg, i + (hasInstanceParameter ? 1 : 0));
                }

                c.Emit(OpCodes.Call, delegateInvokeMethod);
                c.Emit(OpCodes.Ret);
            });

            TDelegate invokeDelegate;
            try
            {
                invokeDelegate = dmd.Generate().CreateDelegate<TDelegate>();
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix($"Failed to create delegate ({typeof(TDelegate).FullName}) for invoke method: {e}");
                return null;
            }

            _invokeDelegateCache.Add(eventDelegateField, invokeDelegate);
            return invokeDelegate;
        }
    }
}
