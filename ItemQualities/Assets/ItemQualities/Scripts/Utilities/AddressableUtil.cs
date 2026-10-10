using HG.Coroutines;
using ItemQualities.Utilities.Extensions;
using RoR2.ContentManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Utilities
{
    internal static class AddressableUtil
    {
        private static readonly Dictionary<Type, AssetAsyncReferenceManagerInstance> _assetAsyncReferenceManagerCache = new Dictionary<Type, AssetAsyncReferenceManagerInstance>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncOperationHandle<T> LoadTempAssetAsync<T>(string assetKey) where T : UnityEngine.Object
        {
            return AssetAsyncReferenceManager<T>.LoadAsset(assetKey, AsyncReferenceHandleUnloadType.Preload);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncOperationHandle<T> LoadTempAssetAsync<T>(AssetReferenceT<T> assetReference) where T : UnityEngine.Object
        {
            return AssetAsyncReferenceManager<T>.LoadAsset(assetReference, AsyncReferenceHandleUnloadType.Preload);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncOperationHandle<T> LoadAssetAsync<T>(string assetKey, AsyncReferenceHandleUnloadType unloadType = AsyncReferenceHandleUnloadType.AtWill) where T : UnityEngine.Object
        {
            return AssetAsyncReferenceManager<T>.LoadAsset(assetKey, unloadType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncOperationHandle<T> LoadAssetAsync<T>(AssetReferenceT<T> assetReference, AsyncReferenceHandleUnloadType unloadType = AsyncReferenceHandleUnloadType.AtWill) where T : UnityEngine.Object
        {
            return AssetAsyncReferenceManager<T>.LoadAsset(assetReference, unloadType);
        }

        public static AsyncOperationHandle LoadAssetAsync(AssetReference assetReference, Type assetType, AsyncReferenceHandleUnloadType unloadType = AsyncReferenceHandleUnloadType.AtWill)
        {
            AssetAsyncReferenceManagerInstance assetAsyncReferenceManager = getOrCreateAssetAsyncReferenceManager(assetType);
            return assetAsyncReferenceManager.LoadAssetAsync(assetReference, unloadType);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UnloadAsset<T>(AssetReferenceT<T> assetReference)
            where T : UnityEngine.Object
        {
            AssetAsyncReferenceManager<T>.UnloadAsset(assetReference);
        }

        public static IEnumerator LoadTempAssetOrDirectReferenceAsync<TAsset, TProgress>(AssetReferenceT<TAsset> assetReference, TAsset directReference, TProgress progressReceiver, CoroutineResult<TAsset> result)
            where TAsset : UnityEngine.Object
            where TProgress : IProgress<float>
        {
            return LoadAssetOrDirectReferenceAsync(assetReference, directReference, progressReceiver, result, AsyncReferenceHandleUnloadType.Preload);
        }

        public static IEnumerator LoadAssetOrDirectReferenceAsync<TAsset, TProgress>(AssetReferenceT<TAsset> assetReference, TAsset directReference, TProgress progressReceiver, CoroutineResult<TAsset> result, AsyncReferenceHandleUnloadType unloadType = AsyncReferenceHandleUnloadType.AtWill)
            where TAsset : UnityEngine.Object
            where TProgress : IProgress<float>
        {
            if (assetReference != null && assetReference.RuntimeKeyIsValid())
            {
                AsyncOperationHandle<TAsset> bfgProjectileGhostPrefabLoad = LoadAssetAsync(assetReference, unloadType);
                yield return bfgProjectileGhostPrefabLoad.AsProgressCoroutine(progressReceiver);

                if (bfgProjectileGhostPrefabLoad.AssertLoaded())
                {
                    result.Value = bfgProjectileGhostPrefabLoad.Result;
                }
            }
            else
            {
                progressReceiver.Report(1f);
                result.Value = directReference;
            }
        }

        public static void UnloadAsset(AssetReference assetReference, Type assetType)
        {
            AssetAsyncReferenceManagerInstance assetAsyncReferenceManager = getOrCreateAssetAsyncReferenceManager(assetType);
            assetAsyncReferenceManager.UnloadAsset(assetReference);
        }

        private static AssetAsyncReferenceManagerInstance getOrCreateAssetAsyncReferenceManager(Type assetType)
        {
            if (!_assetAsyncReferenceManagerCache.TryGetValue(assetType, out AssetAsyncReferenceManagerInstance assetAsyncReferenceManager))
            {
                _assetAsyncReferenceManagerCache.Add(assetType, assetAsyncReferenceManager = new AssetAsyncReferenceManagerInstance(assetType));
            }

            return assetAsyncReferenceManager;
        }

        private sealed class AssetAsyncReferenceManagerInstance
        {
            private static readonly FieldInfo _assetReferenceSubObjectTypeField = typeof(AssetReference).GetField("m_SubObjectType", BindingFlags.Instance | BindingFlags.NonPublic);

            static AssetAsyncReferenceManagerInstance()
            {
                if (_assetReferenceSubObjectTypeField == null)
                {
                    Log.Error($"Failed to find field 'm_SubObjectType' in type 'AssetReference'");
                }
            }

            public Type AssetType { get; }

            private readonly Type _assetAsyncReferenceManagerType;

            private readonly Type _desiredAssetReferenceType;

            private readonly MethodInfo _loadAssetMethod;

            private readonly MethodInfo _unloadAssetMethod;

            private readonly MethodInfo _handleConverterMethod;

            public AssetAsyncReferenceManagerInstance(Type assetType)
            {
                if (assetType == null)
                {
                    throw new ArgumentNullException(nameof(assetType));
                }

                AssetType = assetType;

                _assetAsyncReferenceManagerType = typeof(AssetAsyncReferenceManager<>).MakeGenericType(AssetType);
                _desiredAssetReferenceType = typeof(AssetReferenceT<>).MakeGenericType(AssetType);

                _loadAssetMethod = _assetAsyncReferenceManagerType.GetMethod(nameof(AssetAsyncReferenceManager<UnityEngine.Object>.LoadAsset), new Type[] { _desiredAssetReferenceType, typeof(AsyncReferenceHandleUnloadType) });
                _unloadAssetMethod = _assetAsyncReferenceManagerType.GetMethod(nameof(AssetAsyncReferenceManager<UnityEngine.Object>.UnloadAsset), new Type[] { _desiredAssetReferenceType });

                Type operationHandleType = typeof(AsyncOperationHandle<>).MakeGenericType(AssetType);
                _handleConverterMethod = ReflectionUtil.FindImplicitConverter(operationHandleType, typeof(AsyncOperationHandle));

                if (_loadAssetMethod == null)
                {
                    Log.Error($"Failed to find LoadAsset method for asset type: {AssetType.FullName}");
                }

                if (_unloadAssetMethod == null)
                {
                    Log.Error($"Failed to find UnloadAsset method for asset type: {AssetType.FullName}");
                }

                if (_handleConverterMethod == null)
                {
                    Log.Error($"Failed to find converter method for handle type {operationHandleType.FullName}");
                }
            }

            public AsyncOperationHandle LoadAssetAsync(AssetReference assetReference, AsyncReferenceHandleUnloadType unloadType = AsyncReferenceHandleUnloadType.AtWill)
            {
                if (_loadAssetMethod == null || _handleConverterMethod == null)
                {
                    return default;
                }

                ensureDesiredAssetReferenceType(ref assetReference);

                object loadHandle = _loadAssetMethod.Invoke(null, new object[] { assetReference, unloadType });
                return (AsyncOperationHandle)_handleConverterMethod.Invoke(null, new object[] { loadHandle });
            }

            public void UnloadAsset(AssetReference assetReference)
            {
                if (_unloadAssetMethod == null)
                {
                    return;
                }

                ensureDesiredAssetReferenceType(ref assetReference);

                _unloadAssetMethod.Invoke(null, new object[] { assetReference });
            }

            private void ensureDesiredAssetReferenceType(ref AssetReference assetReference)
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
