using HG;
using System;
using System.Collections.Generic;

namespace ItemQualities.Utilities
{
    public sealed class UnorderedCollectionComparer<T, TCollection> : EqualityComparer<TCollection>
        where TCollection : ICollection<T>
    {
        public override bool Equals(TCollection a, TCollection b)
        {
            if ((a == null) != (b == null))
                return false;

            // a and b are either both null or both non-null
            if (a == null)
                return true;

            if (ReferenceEquals(a, b))
                return true;

            if (a.Count != b.Count)
                return false;

            using var _ = ListPool<T>.RentCollection(out List<T> remainingElementsB);
            remainingElementsB.AddRange(b);

            foreach (T item in a)
            {
                int index = remainingElementsB.IndexOf(item);
                if (index == -1)
                    return false;

                remainingElementsB.RemoveAt(index);
            }

            return remainingElementsB.Count == 0;
        }

        public override int GetHashCode(TCollection collection)
        {
            if (collection == null)
                return 0;

            HashCode hashCode = new HashCode();
            foreach (T item in collection)
            {
                hashCode.Add(item);
            }

            return hashCode.ToHashCode();
        }
    }
}
