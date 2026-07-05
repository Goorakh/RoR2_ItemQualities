//#undef UNITY_EDITOR // Uncomment for testing UnityEventInterface in editor environment

using RoR2;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

#if !UNITY_EDITOR
using System.Reflection;
#endif

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

        public static bool ExpectComponent<T>(this GameObject gameObject, out T component, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            if (gameObject.TryGetComponent(out component))
            {
                return true;
            }

            Log.Error($"Expected component of type {typeof(T).FullName} on object {Util.GetGameObjectHierarchyName(gameObject)}", callerPath, callerMemberName, callerLineNumber);
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ExpectComponent<T>(this Component srcComponent, out T component, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            return srcComponent.gameObject.ExpectComponent(out component, callerPath, callerMemberName, callerLineNumber);
        }

        static void validatePersistentListener(UnityEventBase unityEvent, Delegate action)
        {
            if (unityEvent is null)
                throw new ArgumentNullException(nameof(unityEvent));

            if (action is null)
                throw new ArgumentNullException(nameof(action));

            if (action.Target is not UnityEngine.Object)
                throw new ArgumentException("Invalid action: Listeners must have a UnityEngine.Object instance", nameof(action));

            if (action.Method is null)
                throw new ArgumentException("Invalid action: Listeners cannot be combined delegates.", nameof(action));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddPersistentListener(this UnityEvent unityEvent, UnityAction action)
        {
            validatePersistentListener(unityEvent, action);

#if UNITY_EDITOR
            UnityEditor.Events.UnityEventTools.AddPersistentListener(unityEvent, action);
#else
            UnityEventInterface.AddVoidPersistentListener(unityEvent, action);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddPersistentListener(this UnityEvent unityEvent, UnityAction<string> action, string argument)
        {
            validatePersistentListener(unityEvent, action);

#if UNITY_EDITOR
            UnityEditor.Events.UnityEventTools.AddStringPersistentListener(unityEvent, action, argument);
#else
            UnityEventInterface.AddStringPersistentListener(unityEvent, action, argument);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddPersistentListener<T>(this UnityEvent<T> unityEvent, UnityAction<T> action)
        {
            validatePersistentListener(unityEvent, action);

#if UNITY_EDITOR
            UnityEditor.Events.UnityEventTools.AddPersistentListener(unityEvent, action);
#else
            UnityEventInterface.AddEventPersistentListener(unityEvent, action);
#endif
        }

#if !UNITY_EDITOR
        static class UnityEventInterface
        {
            static readonly FieldInfo _unityEventBasePersistentListenersField;

            static readonly Type _unityPersistentCallGroupType;

            static readonly Type _unityPersistentCallType;

            static readonly MethodInfo _persistentCallGroupAddListenerMethod;

            static readonly MethodInfo _persistentCallGroupRegisterVoidPersistentListenerMethod;
            static readonly MethodInfo _persistentCallGroupRegisterEventPersistentListenerMethod;
            static readonly MethodInfo _persistentCallGroupRegisterStringPersistentListenerMethod;

            static UnityEventInterface()
            {
                _unityEventBasePersistentListenersField = typeof(UnityEventBase).GetField("m_PersistentCalls", BindingFlags.NonPublic | BindingFlags.Instance);

                if (_unityEventBasePersistentListenersField == null)
                {
                    Log.Error("Failed to initialize event interface: Could not find field: UnityEventBase.m_PersistentCalls");
                    return;
                }

                _unityPersistentCallGroupType = _unityEventBasePersistentListenersField.FieldType;

                _persistentCallGroupAddListenerMethod = _unityPersistentCallGroupType.GetMethod("AddListener", BindingFlags.Public | BindingFlags.Instance, null, Array.Empty<Type>(), null);
                if (_persistentCallGroupAddListenerMethod == null)
                {
                    Log.Error("Failed to find PersistentCallGroup.AddListener() method");
                }

                _persistentCallGroupRegisterVoidPersistentListenerMethod = _unityPersistentCallGroupType.GetMethod("RegisterVoidPersistentListener", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(int), typeof(UnityEngine.Object), typeof(Type), typeof(string) }, null);
                if (_persistentCallGroupRegisterVoidPersistentListenerMethod == null)
                {
                    Log.Error("Failed to find PersistentCallGroup.RegisterVoidPersistentListener(int, UnityEngine.Object, Type, string) method");
                }

                _persistentCallGroupRegisterEventPersistentListenerMethod = _unityPersistentCallGroupType.GetMethod("RegisterEventPersistentListener", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(int), typeof(UnityEngine.Object), typeof(Type), typeof(string) }, null);
                if (_persistentCallGroupRegisterEventPersistentListenerMethod == null)
                {
                    Log.Error("Failed to find PersistentCallGroup.RegisterEventPersistentListener(int, UnityEngine.Object, Type, string) method");
                }

                _persistentCallGroupRegisterStringPersistentListenerMethod = _unityPersistentCallGroupType.GetMethod("RegisterStringPersistentListener", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(int), typeof(UnityEngine.Object), typeof(Type), typeof(string), typeof(string) }, null);
                if (_persistentCallGroupRegisterStringPersistentListenerMethod == null)
                {
                    Log.Error("Failed to find PersistentCallGroup.RegisterStringPersistentListener(int, UnityEngine.Object, Type, string, string) method");
                }
            }

            static bool tryRegisterPersistentListener(UnityEventBase unityEvent, out int index, out object persistentCallGroup)
            {
                if (_unityEventBasePersistentListenersField == null ||
                    _persistentCallGroupAddListenerMethod == null)
                {
                    index = -1;
                    persistentCallGroup = default;
                    return false;
                }

                index = unityEvent.GetPersistentEventCount();

                persistentCallGroup = _unityEventBasePersistentListenersField.GetValue(unityEvent);

                _persistentCallGroupAddListenerMethod.Invoke(persistentCallGroup, Array.Empty<object>());

                return true;
            }

            public static void AddVoidPersistentListener(UnityEvent unityEvent, UnityAction action)
            {
                if (_persistentCallGroupRegisterVoidPersistentListenerMethod == null ||
                    !tryRegisterPersistentListener(unityEvent, out int index, out object persistentCallGroup))
                {
                    Log.Error("Failed to add listener: Interface initialization did not succeed for required component(s). Listener will not be persistent.");
                    unityEvent.AddListener(action);
                    return;
                }

                _persistentCallGroupRegisterVoidPersistentListenerMethod.Invoke(persistentCallGroup, new object[]
                {
                    index,
                    action.Target as UnityEngine.Object,
                    action.Target.GetType(),
                    action.Method.Name
                });

                unityEvent.SetPersistentListenerState(index, UnityEventCallState.RuntimeOnly);
            }

            public static void AddEventPersistentListener<TDelegate>(UnityEventBase unityEvent, TDelegate action)
                where TDelegate : Delegate
            {
                if (_persistentCallGroupRegisterEventPersistentListenerMethod == null ||
                    !tryRegisterPersistentListener(unityEvent, out int index, out object persistentCallGroup))
                {
                    Log.Error("Failed to add listener: Interface initialization did not succeed for required component(s).");
                    return;
                }

                _persistentCallGroupRegisterEventPersistentListenerMethod.Invoke(persistentCallGroup, new object[]
                {
                    index,
                    action.Target as UnityEngine.Object,
                    action.Target.GetType(),
                    action.Method.Name
                });

                unityEvent.SetPersistentListenerState(index, UnityEventCallState.RuntimeOnly);
            }

            static void addArgumentPersistentListener<T>(UnityEvent unityEvent, MethodInfo registerMethod, UnityAction<T> action, T argument)
            {
                if (registerMethod == null ||
                    !tryRegisterPersistentListener(unityEvent, out int index, out object persistentCallGroup))
                {
                    Log.Error($"Failed to add listener of type {typeof(T).FullName}: Interface initialization did not succeed for required component(s).");
                    return;
                }

                registerMethod.Invoke(persistentCallGroup, new object[]
                {
                    index,
                    action.Target as UnityEngine.Object,
                    action.Target.GetType(),
                    argument,
                    action.Method.Name
                });

                unityEvent.SetPersistentListenerState(index, UnityEventCallState.RuntimeOnly);
            }

            public static void AddStringPersistentListener(UnityEvent unityEvent, UnityAction<string> action, string argument)
            {
                addArgumentPersistentListener(unityEvent, _persistentCallGroupRegisterStringPersistentListenerMethod, action, argument);
            }
        }
#endif
    }
}
