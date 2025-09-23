using System;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Utilities
{
    public readonly struct ScopedAsyncOperationHandle : IDisposable
    {
        readonly AsyncOperationHandle _handle;

        public ScopedAsyncOperationHandle(AsyncOperationHandle handle)
        {
            _handle = handle;
        }

        public void Dispose()
        {
            Addressables.Release(_handle);
        }
    }

    public readonly struct ScopedAsyncOperationHandle<T> : IDisposable
    {
        readonly AsyncOperationHandle<T> _handle;

        public ScopedAsyncOperationHandle(AsyncOperationHandle<T> handle)
        {
            _handle = handle;
        }

        public void Dispose()
        {
            Addressables.Release(_handle);
        }
    }
}
