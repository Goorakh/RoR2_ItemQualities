using System.Collections.Generic;

namespace ItemQualities.Utilities
{
    internal sealed class CollectionComparer<T> : IComparer<ICollection<T>>
    {
        public static CollectionComparer<T> SizeAscending { get; } = new CollectionComparer<T>(false);

        public static CollectionComparer<T> SizeDescending { get; } = new CollectionComparer<T>(true);

        readonly bool _descending;

        private CollectionComparer(bool descending)
        {
            _descending = descending;
        }

        public int Compare(ICollection<T> x, ICollection<T> y)
        {
            int xSize = x != null ? x.Count : -1;
            int ySize = y != null ? y.Count : -1;

            return _descending ? ySize - xSize : xSize - ySize;
        }
    }
}
