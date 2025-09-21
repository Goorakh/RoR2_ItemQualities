using RoR2.ContentManagement;
using System.Runtime.CompilerServices;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Utilities
{
    public static class AddressableUtil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncOperationHandle<T> LoadTempAssetAsync<T>(string assetKey) where T : UnityEngine.Object
        {
            return LoadTempAssetAsync(new AssetReferenceT<T>(assetKey));
        }

        public static AsyncOperationHandle<T> LoadTempAssetAsync<T>(AssetReferenceT<T> assetReference) where T : UnityEngine.Object
        {
            AsyncOperationHandle<T> loadHandle = AssetAsyncReferenceManager<T>.LoadAsset(assetReference);
            loadHandle.Completed += handle =>
            {
                AssetAsyncReferenceManager<T>.UnloadAsset(assetReference);
            };

            return loadHandle;
        }
    }
}
