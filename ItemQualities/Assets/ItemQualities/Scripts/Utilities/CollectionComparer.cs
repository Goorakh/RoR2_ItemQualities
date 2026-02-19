using System.Collections;
using System.Collections.Generic;

namespace ItemQualities.Utilities
{
    internal sealed class CollectionComparer : IComparer<ICollection>
    {
        public static CollectionComparer SizeAscending { get; } = new CollectionComparer(false);

        public static CollectionComparer SizeDescending { get; } = new CollectionComparer(true);

        readonly bool _descending;

        private CollectionComparer(bool descending)
        {
            _descending = descending;
        }

        public int Compare(ICollection x, ICollection y)
        {
            int xSize = x != null ? x.Count : -1;
            int ySize = y != null ? y.Count : -1;

            return _descending ? ySize - xSize : xSize - ySize;
        }
    }
}
