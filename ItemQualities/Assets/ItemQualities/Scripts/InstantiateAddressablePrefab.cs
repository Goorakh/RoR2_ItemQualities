using RoR2.ContentManagement;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ItemQualities
{
    [DefaultExecutionOrder(-1)]
    public sealed class InstantiateAddressablePrefab : MonoBehaviour
    {
        [SerializeField]
        private Transform _parent;

        [SerializeField]
        private AssetReferenceGameObject _prefabAddress = new AssetReferenceGameObject(string.Empty);

        [SerializeField]
        private bool _allowAsyncLoad = false;

        [SerializeField]
        private AsyncReferenceHandleUnloadType _prefabUnloadType = AsyncReferenceHandleUnloadType.OnSceneUnload;

        [SerializeField]
        private bool _instantiateOnAwake = true;

        private GameObject _createdInstance;

        private readonly AssetOrDirectReference<GameObject> _prefabReference = new AssetOrDirectReference<GameObject>();

        public event Action<GameObject> OnInstantiated;
        public event Action OnInstanceDestroyed;

        private void Awake()
        {
            _prefabReference.unloadType = _prefabUnloadType;
            _prefabReference.address = _prefabAddress;

            if (_instantiateOnAwake)
            {
                InstantiatePrefab();
            }
        }

        private void OnDestroy()
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

        private void onPrefabReferenceDiscovered(GameObject prefab)
        {
            instantiatePrefab(prefab);
        }

        private void onPrefabReferenceLost(GameObject prefab)
        {
            destroyInstance();
        }

        private void instantiatePrefab(GameObject prefab)
        {
            if (_createdInstance)
            {
                Log.Warning("Attempting to instantiate prefab multiple times");
                return;
            }

            _createdInstance = Instantiate(prefab, _parent);

            OnInstantiated?.Invoke(_createdInstance);
        }

        private void destroyInstance()
        {
            if (!_createdInstance)
                return;

            Destroy(_createdInstance);
            _createdInstance = null;

            OnInstanceDestroyed?.Invoke();
        }
    }
}
