using System.Collections.Generic;

namespace ItemQualities.Utilities.Extensions
{
    internal static class WeightedSelectionExtensions
    {
        public static void EnsureCapacity<T>(this WeightedSelection<T> selection, int capacity)
        {
            if (selection.Capacity < capacity)
            {
                selection.Capacity = capacity;
            }
        }

        public static int FindChoiceIndex<T>(this WeightedSelection<T> selection, in T value)
        {
            return selection.FindChoiceIndex(value, EqualityComparer<T>.Default);
        }

        public static int FindChoiceIndex<T, TComparer>(this WeightedSelection<T> selection, in T value, in TComparer comparer)
            where TComparer : IEqualityComparer<T>
        {
            for (int i = 0; i < selection.Count; i++)
            {
                WeightedSelection<T>.ChoiceInfo choiceInfo = selection.GetChoice(i);
                if (comparer.Equals(choiceInfo.value, value))
                {
                    return i;
                }
            }

            return -1;
        }

        public static void AddTo<T>(this WeightedSelection<T> src, WeightedSelection<T> dest)
        {
            src.AddTo(dest, EqualityComparer<T>.Default);
        }

        public static void AddTo<T, TComparer>(this WeightedSelection<T> src, WeightedSelection<T> dest, in TComparer comparer)
            where TComparer : IEqualityComparer<T>
        {
            if (src.Count > 0)
            {
                dest.EnsureCapacity(dest.Count + src.Count);
                for (int i = 0; i < src.Count; i++)
                {
                    WeightedSelection<T>.ChoiceInfo srcChoiceInfo = src.GetChoice(i);
                    int destChoiceIndex = dest.FindChoiceIndex(srcChoiceInfo.value, comparer);
                    if (destChoiceIndex != -1)
                    {
                        WeightedSelection<T>.ChoiceInfo destChoiceInfo = dest.GetChoice(destChoiceIndex);
                        dest.ModifyChoiceWeight(destChoiceIndex, destChoiceInfo.weight + srcChoiceInfo.weight);
                    }
                    else
                    {
                        dest.AddChoice(srcChoiceInfo.value, srcChoiceInfo.weight);
                    }
                }
            }
        }
    }
}
