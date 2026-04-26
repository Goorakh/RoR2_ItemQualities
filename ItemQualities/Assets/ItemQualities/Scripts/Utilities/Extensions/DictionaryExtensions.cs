using System.Collections.Generic;

namespace ItemQualities.Utilities.Extensions
{
    internal static class DictionaryExtensions
    {
        public static TValue GetOrAddNew<TDict, TKey, TValue>(this TDict dictionary, TKey key)
            where TDict : IDictionary<TKey, TValue>
            where TValue : new()
        {
            if (!dictionary.TryGetValue(key, out TValue value))
            {
                dictionary.Add(key, value = new TValue());
            }

            return value;
        }
    }
}
