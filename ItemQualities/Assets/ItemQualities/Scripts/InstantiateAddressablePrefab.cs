using RoR2.ContentManagement;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ItemQualities
{
    [DefaultExecutionOrder(-1)]
    public class InstantiateAddressablePrefab : MonoBehaviour
    {
        [SerializeField]
        Transform _parent;

        [SerializeField]
        AssetReferenceGameObject _prefabAddress = new AssetReferenceGameObject(string.Empty);

        [SerializeField]
        bool _allowAsyncLoad = false;

        [SerializeField]
        AsyncReferenceHandleUnloadType _prefabUnloadType = AsyncReferenceHandleUnloadType.OnSceneUnload;

        [SerializeField]
        bool _instantiateOnAwake = true;

        GameObject _createdInstance;

        readonly AssetOrDirectReference<GameObject> _prefabReference = new AssetOrDirectReference<GameObject>();

        public event Action<GameObject> OnInstantiated;
        public event Action OnInstanceDestroyed;

        void Awake()
        {
            _prefabReference.unloadType = _prefabUnloadType;
            _prefabReference.address = _prefabAddress;

            if (_instantiateOnAwake)
            {
                InstantiatePrefab();
            }
        }

        void OnDestroy()
        {
            _prefabReference.Reset();

            _prefabReference.onValidReferenceDiscovered -= onPrefabReferenceDiscovered;
            _prefabReference.onValidReferenceLost -= onPrefabReferenceLost;

            destroyInstance();
        }

        public void InstantiatePrefab()
        {
            if (!_prefabReference.IsLoaded() && _allowAsyncLoad)
            {
                _prefabReference.onValidReferenceDiscovered += onPrefabReferenceDiscovered;
                _prefabReference.onValidReferenceLost += onPrefabReferenceLost;
            }
            else
            {
                GameObject prefab = _prefabReference.WaitForCompletion();
                instantiatePrefab(prefab);
            }
        }

        void onPrefabReferenceDiscovered(GameObject prefab)
        {
            instantiatePrefab(prefab);
        }

        void onPrefabReferenceLost(GameObject prefab)
        {
            destroyInstance();
        }

        void instantiatePrefab(GameObject prefab)
        {
            if (_createdInstance)
            {
                Log.Warning("Attempting to instantiate prefab multiple times");
                return;
            }

            _createdInstance = Instantiate(prefab, _parent);

            OnInstantiated?.Invoke(_createdInstance);
        }

        void destroyInstance()
        {
            if (!_createdInstance)
                return;

            Destroy(_createdInstance);
            _createdInstance = null;

            OnInstanceDestroyed?.Invoke();
        }
    }
}
