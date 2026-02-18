using System;

namespace ItemQualities.Utilities
{
    static class GenericSingletonHelper
    {
        public static bool Assign<T>(ref T instance, T value) where T : class
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            if (instance == null)
            {
                instance = value;
                return true;
            }

            Log.Error($"Duplicate instance of singleton class {typeof(T).FullName}");
            return false;
        }

        public static T Assign<T>(T existingInstance, T value) where T : class
        {
            Assign(ref existingInstance, value);
            return existingInstance;
        }

        public static bool Unassign<T>(ref T field, T instance) where T : class
        {
            if (instance is null)
                throw new ArgumentNullException(nameof(instance));

            if (ReferenceEquals(field, instance))
            {
                field = null;
                return true;
            }

            return false;
        }

        public static T Unassign<T>(T existingInstance, T instance) where T : class
        {
            Unassign(ref existingInstance, instance);
            return existingInstance;
        }
    }
}
