using RoR2;
using System;
using UnityEngine;

namespace ItemQualities
{
    internal sealed class LocalEffectOwnership : MonoBehaviour
    {
        EffectComponent _effectComponent;

        GameObject _ownerObject;
        public GameObject OwnerObject
        {
            get
            {
                return _ownerObject;
            }
            set
            {
                if (_ownerObject == value)
                    return;

                _ownerObject = value;
                OnOwnerChanged?.Invoke(_ownerObject);
            }
        }

        public event Action<GameObject> OnOwnerChanged;

        void Awake()
        {
            _effectComponent = GetComponent<EffectComponent>();

            if (_effectComponent)
            {
                _effectComponent.OnEffectComponentReset += onReset;
            }
        }

        void OnDestroy()
        {
            if (_effectComponent)
            {
                _effectComponent.OnEffectComponentReset -= onReset;
            }
        }

        void onReset(bool hasEffectData)
        {
            OwnerObject = hasEffectData && _effectComponent.effectData != null ? _effectComponent.effectData.ResolveNetworkedObjectReference() : null;
        }
    }
}
