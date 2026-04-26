using System.Collections;
using System.Collections.Generic;

namespace ItemQualities.Utilities
{
    internal sealed class CollectionComparer<TCollection> : Comparer<TCollection>
        where TCollection : ICollection
    {
        public static CollectionComparer<TCollection> SizeAscending { get; } = new CollectionComparer<TCollection>(false);

        public static CollectionComparer<TCollection> SizeDescending { get; } = new CollectionComparer<TCollection>(true);

        readonly bool _descending;

        private CollectionComparer(bool descending)
        {
            _descending = descending;
        }

        public override int Compare(TCollection x, TCollection y)
        {
            int xSize = x != null ? x.Count : -1;
            int ySize = y != null ? y.Count : -1;

            return _descending ? ySize - xSize : xSize - ySize;
        }
    }
}
