using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

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

        public static void AddPersistentListener(this UnityEvent unityEvent, UnityAction action)
        {
            if (unityEvent is null)
                throw new ArgumentNullException(nameof(unityEvent));

            if (action is null)
                throw new ArgumentNullException(nameof(action));

            if (action.Target is not UnityEngine.Object)
                throw new ArgumentException("Invalid action: Listeners must have a UnityEngine.Object instance", nameof(action));

            if (action.Method == null)
                throw new ArgumentException("Invalid action: Listeners cannot be combined delegates.", nameof(action));

#if UNITY_EDITOR
            UnityEditor.Events.UnityEventTools.AddPersistentListener(unityEvent, action);
#else
            _eventInterfaceInstance ??= new UnityEventInterface();
            _eventInterfaceInstance.RegisterVoidPersistentListener(unityEvent, action);
#endif
        }

        static UnityEventInterface _eventInterfaceInstance;

        sealed class UnityEventInterface
        {
            readonly FieldInfo _unityEventBasePersistentListenersField;

            readonly Type _unityPersistentCallGroupType;

            readonly Type _unityPersistentCallType;

            readonly MethodInfo _persistentCallGroupAddListenerMethod;
            readonly MethodInfo _persistentCallGroupRegisterVoidPersistentListenerMethod;

            public UnityEventInterface()
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
            }

            public void RegisterVoidPersistentListener(UnityEvent unityEvent, UnityAction action)
            {
                if (_unityEventBasePersistentListenersField == null ||
                    _persistentCallGroupAddListenerMethod == null ||
                    _persistentCallGroupRegisterVoidPersistentListenerMethod == null)
                {
                    Log.Error("Failed to add listener: Interface initialization did not succeed for required component(s). Listener will not be persistent.");
                    unityEvent.AddListener(action);
                    return;
                }

                int index = unityEvent.GetPersistentEventCount();

                object persistentCallGroup = _unityEventBasePersistentListenersField.GetValue(unityEvent);

                _persistentCallGroupAddListenerMethod.Invoke(persistentCallGroup, Array.Empty<object>());

                _persistentCallGroupRegisterVoidPersistentListenerMethod.Invoke(persistentCallGroup, new object[]
                {
                    index,
                    action.Target as UnityEngine.Object,
                    action.Target.GetType(),
                    action.Method.Name
                });
            }
        }
    }
}
