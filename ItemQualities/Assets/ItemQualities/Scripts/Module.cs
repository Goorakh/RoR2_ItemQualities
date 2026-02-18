using HG.Reflection;
using ItemQualities.Utilities;
using System;
using System.Runtime.CompilerServices;

namespace ItemQualities
{
    public abstract class Module<T> : IModule, IDisposable where T : Module<T>, new()
    {
        static T _instance;
        public static T Instance => _instance;

        protected Module()
        {
            if (this is not T thisT)
                throw new ArgumentException($"Module type {GetType().FullName} must be assignable to singleton type {typeof(T).FullName}");

            if (GenericSingletonHelper.Assign(ref _instance, thisT))
            {
                OnEnable();
            }
        }

        public void Dispose()
        {
            if (this is T thisT && GenericSingletonHelper.Unassign(ref _instance, thisT))
            {
                OnDisable();
            }
        }

        void IModule.OnEnable() => OnEnable();

        void IModule.OnDisable() => OnDisable();

        protected virtual void OnEnable()
        {
        }

        protected virtual void OnDisable()
        {
        }
    }

    public interface IModule
    {
        void OnEnable()
        {
        }

        void OnDisable()
        {

        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class ModuleInfoAttribute : SearchableAttribute
    {
        public Type ModuleType
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Type)target;
        }

        public Type[] Dependencies { get; init; } = Array.Empty<Type>();

        public ModuleInfoAttribute(string moduleName)
        {

        }

        public bool IsEnabled()
        {
            foreach (Type dependencyType in Dependencies)
            {

            }

            return true;
        }
    }

    [ModuleInfo(Dependencies = new Type[] { typeof(Test2Module) })]
    public sealed class TestModule : Module<TestModule>
    {
    }

    public sealed class Test2Module : Module<TestModule>
    {
    }
}
