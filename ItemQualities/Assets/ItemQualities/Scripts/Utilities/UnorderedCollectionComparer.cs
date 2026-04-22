using HG;
using ItemQualities.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ItemQualities.Utilities
{
    public sealed class UnorderedCollectionComparer<T> : IEqualityComparer<IEnumerable<T>>
    {
        public static UnorderedCollectionComparer<T> Default { get; } = new UnorderedCollectionComparer<T>();

        public IEqualityComparer<T> ElementComparer { get; set; } = EqualityComparer<T>.Default;

        public bool Equals(IEnumerable<T> a, IEnumerable<T> b)
        {
            if ((a == null) != (b == null))
                return false;

            // a and b are either both null or both non-null
            if (a == null)
                return true;

            if (ReferenceEquals(a, b))
                return true;

            int collectionSize = a.Count();
            if (collectionSize != b.Count())
                return false;

            using var _ = ListPool<T>.RentCollection(out List<T> remainingElementsB);
            remainingElementsB.EnsureCapacity(collectionSize);
            remainingElementsB.AddRange(b);

            foreach (T item in a)
            {
                int index = remainingElementsB.IndexOf<T, List<T>>(item, ElementComparer);
                if (index == -1)
                    return false;

                remainingElementsB.RemoveAt(index);
            }

            return true;
        }

        public int GetHashCode(IEnumerable<T> collection)
        {
            if (collection == null)
                return 0;

            HashCode hashCode = new HashCode();
            foreach (T item in collection)
            {
                hashCode.Add(item, ElementComparer);
            }

            return hashCode.ToHashCode();
        }
    }
}
