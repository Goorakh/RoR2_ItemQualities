using RoR2;
using System;
using UnityEngine;

namespace ItemQualities
{
    internal sealed class LocalEffectOwnership : MonoBehaviour
    {
        private EffectComponent _effectComponent;
        private EffectManagerHelper _effectManagerHelper;

        private GameObject _ownerObject;
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

        private void Awake()
        {
            _effectComponent = GetComponent<EffectComponent>();
            _effectManagerHelper = GetComponent<EffectManagerHelper>();

            if (_effectComponent)
            {
                _effectComponent.OnEffectComponentReset += onReset;
            }
            else if (_effectManagerHelper)
            {
                _effectManagerHelper.OnEffectActivated += onActivated;
            }
        }

        private void OnDestroy()
        {
            if (_effectComponent)
            {
                _effectComponent.OnEffectComponentReset -= onReset;
            }
            else if (_effectManagerHelper)
            {
                _effectManagerHelper.OnEffectActivated -= onActivated;
            }
        }

        private void onReset(bool hasEffectData)
        {
            OwnerObject = hasEffectData && _effectComponent.effectData != null ? _effectComponent.effectData.ResolveNetworkedObjectReference() : null;
        }

        private void onActivated()
        {
            OwnerObject = null;
        }
    }
}
