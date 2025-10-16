using RoR2.ContentManagement;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Utilities
{
    public static class AddressableUtil
    {
        static readonly Dictionary<Type, AssetAsyncReferenceManagerInstance> _assetAsyncReferenceManagerCache = new Dictionary<Type, AssetAsyncReferenceManagerInstance>();

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

        public static AsyncOperationHandle LoadAssetAsync(AssetReference assetReference, Type assetType, AsyncReferenceHandleUnloadType unloadType = AsyncReferenceHandleUnloadType.AtWill)
        {
            AssetAsyncReferenceManagerInstance assetAsyncReferenceManager = getOrCreateAssetAsyncReferenceManager(assetType);
            return assetAsyncReferenceManager.LoadAssetAsync(assetReference, unloadType);
        }

        public static void UnloadAsset(AssetReference assetReference, Type assetType)
        {
            AssetAsyncReferenceManagerInstance assetAsyncReferenceManager = getOrCreateAssetAsyncReferenceManager(assetType);
            assetAsyncReferenceManager.UnloadAsset(assetReference);
        }
        
        static AssetAsyncReferenceManagerInstance getOrCreateAssetAsyncReferenceManager(Type assetType)
        {
            if (!_assetAsyncReferenceManagerCache.TryGetValue(assetType, out AssetAsyncReferenceManagerInstance assetAsyncReferenceManager))
            {
                _assetAsyncReferenceManagerCache.Add(assetType, assetAsyncReferenceManager = new AssetAsyncReferenceManagerInstance(assetType));
            }

            return assetAsyncReferenceManager;
        }

        class AssetAsyncReferenceManagerInstance
        {
            static readonly FieldInfo _assetReferenceSubObjectTypeField = typeof(AssetReference).GetField("m_SubObjectType", BindingFlags.Instance | BindingFlags.NonPublic);

            public Type AssetType { get; }

            readonly Type _assetAsyncReferenceManagerType;

            readonly Type _desiredAssetReferenceType;

            readonly MethodInfo _loadAssetMethod;

            readonly MethodInfo _unloadAssetMethod;

            readonly MethodInfo _handleConverterMethod;

            public AssetAsyncReferenceManagerInstance(Type assetType)
            {
                AssetType = assetType;

                _assetAsyncReferenceManagerType = typeof(AssetAsyncReferenceManager<>).MakeGenericType(AssetType);
                _desiredAssetReferenceType = typeof(AssetReferenceT<>).MakeGenericType(AssetType);

                _loadAssetMethod = _assetAsyncReferenceManagerType.GetMethod(nameof(AssetAsyncReferenceManager<UnityEngine.Object>.LoadAsset));
                _unloadAssetMethod = _assetAsyncReferenceManagerType.GetMethod(nameof(AssetAsyncReferenceManager<UnityEngine.Object>.UnloadAsset));

                _handleConverterMethod = ReflectionUtil.FindImplicitConverter(typeof(AsyncOperationHandle<>).MakeGenericType(assetType), typeof(AsyncOperationHandle));
            }

            public AsyncOperationHandle LoadAssetAsync(AssetReference assetReference, AsyncReferenceHandleUnloadType unloadType = AsyncReferenceHandleUnloadType.AtWill)
            {
                ensureDesiredAssetReferenceType(ref assetReference);

                object loadHandle = _loadAssetMethod.Invoke(null, new object[] { assetReference, unloadType });
                return (AsyncOperationHandle)_handleConverterMethod.Invoke(null, new object[] { loadHandle });
            }

            public void UnloadAsset(AssetReference assetReference)
            {
                ensureDesiredAssetReferenceType(ref assetReference);

                _unloadAssetMethod.Invoke(null, new object[] { assetReference });
            }

            void ensureDesiredAssetReferenceType(ref AssetReference assetReference)
            {
                if (assetReference == null)
                    return;

                Type type = assetReference.GetType();
                if (_desiredAssetReferenceType.IsAssignableFrom(type))
                    return;

                string assetGuid = assetReference.AssetGUID;
                string subObjectName = assetReference.SubObjectName;
                string subObjectType = _assetReferenceSubObjectTypeField.GetValue(assetReference) as string;

                assetReference = (AssetReference)Activator.CreateInstance(_desiredAssetReferenceType, new object[] { assetGuid });
                assetReference.SubObjectName = subObjectName;
                _assetReferenceSubObjectTypeField.SetValue(assetReference, subObjectType);
            }
        }
    }
}
