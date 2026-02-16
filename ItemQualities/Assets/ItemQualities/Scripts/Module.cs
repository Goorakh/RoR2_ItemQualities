using HG.Reflection;
using System;

namespace ItemQualities
{
    internal abstract class Module
    {
        protected virtual Type[] requiredModuleTypes { get; } = Array.Empty<Type>();

        protected virtual bool ShouldLoad()
        {
            return true;
        }

        protected virtual void OnEnable()
        {
        }

        protected virtual void OnDisable()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    internal sealed class ModuleInfoAttribute : SearchableAttribute
    {

    }
}
