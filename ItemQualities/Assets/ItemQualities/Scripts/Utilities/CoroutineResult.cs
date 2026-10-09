using HG;

namespace ItemQualities.Utilities
{
    internal class CoroutineResult<T>
    {
        public T Value;

        public void Reset()
        {
            Value = default;
        }
    }

    internal sealed class CoroutineResultPool<T> : BasePool<CoroutineResult<T>>
    {
        public static readonly CoroutineResultPool<T> instance = new CoroutineResultPool<T>();

        private CoroutineResultPool()
        {
        }

        protected override void ResetItem(CoroutineResult<T> item)
        {
            item.Reset();
        }
    }
}
