using HG;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using RoR2;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace ItemQualities
{
    [DefaultExecutionOrder(-1000)]
    public class AssignAddressableReferences : MonoBehaviour, IAsyncContentLoadCallback
    {
        public ComponentFieldAddressableAssignment[] FieldAssignments = Array.Empty<ComponentFieldAddressableAssignment>();

        [SerializeField]
        [HideInInspector]
        bool _hasLoaded = false;

        public bool HasLoaded => _hasLoaded;

        void Awake()
        {
            if (!_hasLoaded)
            {
                Log.Warning($"{Util.GetGameObjectHierarchyName(gameObject)}: Addressable references were not assigned to prefab during init, loading assets now");

                IEnumerator assignFieldsOperation = AssignFieldsAsync();
                while (assignFieldsOperation.MoveNext())
                {
                }
            }
        }

        IEnumerator IAsyncContentLoadCallback.OnContentLoad(IProgress<float> progressReceiver)
        {
            return AssignFieldsAsync(progressReceiver);
        }

        public IEnumerator AssignFieldsAsync(IProgress<float> progressReceiver = null)
        {
            if (FieldAssignments.Length > 0)
            {
                if (progressReceiver != null)
                {
                    ParallelProgressCoroutine parallelCoroutine = new ParallelProgressCoroutine(progressReceiver);
                    foreach (ComponentFieldAddressableAssignment componentFieldAssignment in FieldAssignments)
                    {
                        ReadableProgress<float> coroutineProgress = new ReadableProgress<float>();
                        parallelCoroutine.Add(assignComponentFieldAsync(componentFieldAssignment, coroutineProgress), coroutineProgress);
                    }

                    yield return parallelCoroutine;
                }
                else
                {
                    ParallelCoroutine parallelCoroutine = new ParallelCoroutine();
                    foreach (ComponentFieldAddressableAssignment componentFieldAssignment in FieldAssignments)
                    {
                        parallelCoroutine.Add(assignComponentFieldAsync(componentFieldAssignment));
                    }

                    yield return parallelCoroutine;
                }
            }

            _hasLoaded = true;
            enabled = false;
        }

        IEnumerator assignComponentFieldAsync(ComponentFieldAddressableAssignment componentFieldAssignment, IProgress<float> progressReceiver = null)
        {
            if (!componentFieldAssignment.TargetObject)
                yield break;

            Type componentType = componentFieldAssignment.TargetObject.GetType();

            MemberInfo targetMember = findTargetMember(componentType, componentFieldAssignment.FieldName, out Type targetMemberType);
            if (targetMember == null)
            {
                Log.Error($"Failed to find field '{componentFieldAssignment.FieldName}' on component {componentFieldAssignment.TargetObject}");
                yield break;
            }

            Type assetType = (Type)componentFieldAssignment.AssetTypeOverride ?? targetMemberType;

            object assetKey = componentFieldAssignment.AssetReference.RuntimeKey;

            AsyncOperationHandle<IList<IResourceLocation>> resourceLocationsLoad = Addressables.LoadResourceLocationsAsync(assetKey, assetType);
            using var resourceLocationsLoadScope = new ScopedAsyncOperationHandle<IList<IResourceLocation>>(resourceLocationsLoad);

            while (!resourceLocationsLoad.IsDone)
            {
                yield return null;
                progressReceiver?.Report(resourceLocationsLoad.PercentComplete / 2f);
            }

            IList<IResourceLocation> resourceLocations = resourceLocationsLoad.Result;
            if (resourceLocations == null || resourceLocations.Count == 0)
            {
                Log.Error($"{Util.GetGameObjectHierarchyName(gameObject)} ({componentType.FullName}.{componentFieldAssignment.FieldName}): Failed to load resoure location for asset {assetKey}");
                yield break;
            }

            AsyncOperationHandle<UnityEngine.Object> assetLoadHandle = Addressables.LoadAssetAsync<UnityEngine.Object>(resourceLocations[0]);
            using var assetLoadScope = new ScopedAsyncOperationHandle(assetLoadHandle);

            while (!assetLoadHandle.IsDone)
            {
                yield return null;
                progressReceiver?.Report(0.5f + (assetLoadHandle.PercentComplete / 2f));
            }

            UnityEngine.Object asset = assetLoadHandle.Result;

            if (!asset)
            {
                Log.Warning($"{Util.GetGameObjectHierarchyName(gameObject)} ({componentType.FullName}.{componentFieldAssignment.FieldName}): Null asset loaded for asset {assetKey}");
            }
            else
            {
                Log.Debug($"{Util.GetGameObjectHierarchyName(gameObject)} ({componentType.FullName}.{componentFieldAssignment.FieldName}): Loaded asset {asset} from {assetKey}");
            }

            try
            {
                switch (targetMember)
                {
                    case FieldInfo field:
                        field.SetValue(componentFieldAssignment.TargetObject, asset);
                        break;
                    case PropertyInfo property:
                        property.SetValue(componentFieldAssignment.TargetObject, asset);
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix($"{Util.GetGameObjectHierarchyName(gameObject)}: Failed to set field {componentType.FullName}.{componentFieldAssignment.FieldName} to asset {asset} from {assetKey}: {e}");
            }
        }

        void OnValidate()
        {
            foreach (ComponentFieldAddressableAssignment componentFieldAssignment in FieldAssignments)
            {
                if (!componentFieldAssignment.TargetObject)
                    continue;

                Type componentType = componentFieldAssignment.TargetObject.GetType();
                MemberInfo targetMember = findTargetMember(componentType, componentFieldAssignment.FieldName, out Type targetMemberType);
                if (targetMember == null)
                {
                    Debug.LogWarning($"Field or property '{componentFieldAssignment.FieldName}' does not exist in type {componentType.FullName}", this);
                    continue;
                }

                if (!typeof(UnityEngine.Object).IsAssignableFrom(targetMemberType))
                {
                    Debug.LogWarning($"Invalid type {targetMemberType.FullName} on field {componentType.FullName}.{componentFieldAssignment.FieldName} (must be UnityEngine.Object)", this);
                }

                Type assetType = (Type)componentFieldAssignment.AssetTypeOverride;
                if (assetType != null && !targetMemberType.IsAssignableFrom(assetType))
                {
                    Debug.LogWarning($"Asset type {assetType.FullName} cannot be assigned to field of type {targetMemberType.FullName} ({componentType.FullName}.{componentFieldAssignment.FieldName})", this);
                }
            }
        }

        static MemberInfo findTargetMember(Type componentType, string fieldName, out Type memberType)
        {
            FieldInfo field = componentType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                memberType = field.FieldType;
                return field;
            }

            PropertyInfo property = componentType.GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                memberType = property.PropertyType;
                return property;
            }

            memberType = null;
            return null;
        }

        [Serializable]
        public class ComponentFieldAddressableAssignment
        {
            [Tooltip("The object to assign the field on")]
            public UnityEngine.Object TargetObject;

            [Tooltip("The name of the field or property to set")]
            public string FieldName;

            [Tooltip("Address of the asset to load")]
            public AssetReferenceT<UnityEngine.Object> AssetReference = new AssetReferenceT<UnityEngine.Object>(string.Empty);

            [Tooltip("Determines what type is used to load the asset, if not set, the type of the field/property is used")]
            [SerializableSystemType.RequiredBaseType(typeof(UnityEngine.Object))]
            public SerializableSystemType AssetTypeOverride;
        }
    }
}
