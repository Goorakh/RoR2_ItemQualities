using System;
using System.Collections.Generic;

namespace ItemQualities.Extensions
{
    internal static class UnityUtils
    {
        public static void SortObjectsByName<T, TComparer>(T[] objects, TComparer nameComparer)
            where T : UnityEngine.Object
            where TComparer : IComparer<string>
        {
            string[] keys = new string[objects.Length];
            for (int i = 0; i < objects.Length; i++)
            {
                keys[i] = objects[i].name;
            }

            Array.Sort(keys, objects, nameComparer);
        }
    }
}
