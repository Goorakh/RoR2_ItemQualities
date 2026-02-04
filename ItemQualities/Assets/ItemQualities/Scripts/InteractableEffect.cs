using RoR2;
using System;
using UnityEngine;

namespace ItemQualities
{
    [RequireComponent(typeof(EffectComponent))]
    public sealed class InteractableEffect : MonoBehaviour
    {
        public GameObject[] ChildObjectsToDisable = Array.Empty<GameObject>();

        public ParticleSystem[] ParticleSystems = Array.Empty<ParticleSystem>();

        EffectComponent _effectComponent;

        GameObject _attachedToObject;

        SpecialObjectAttributes _attachedToObjectAttributes;
        PurchaseInteraction _attachedToPurchaseInteraction;
        BarrelInteraction _attachedToBarrelInteraction;
        SpeedOnPickupBarrelInteraction _attachedToSpeedOnPickupBarrelInteraction;
        MultiShopController _attachedToMultiShopController;
        DroneVendorMultiShopController _attachedToDroneVendorMultiShopController;

        float _particleStateSyncTimer = 0f;

        void Awake()
        {
            _effectComponent = GetComponent<EffectComponent>();
            _effectComponent.OnEffectComponentReset += onReset;
        }

        void OnDisable()
        {
            setAttachedObject(null);
        }

        void FixedUpdate()
        {
            _particleStateSyncTimer -= Time.fixedDeltaTime;
            if (_particleStateSyncTimer <= 0f)
            {
                _particleStateSyncTimer = 0.5f;

                if ((_attachedToPurchaseInteraction && !_attachedToPurchaseInteraction.available) ||
                    (_attachedToBarrelInteraction && _attachedToBarrelInteraction.opened) ||
                    (_attachedToSpeedOnPickupBarrelInteraction && _attachedToSpeedOnPickupBarrelInteraction.IsOpened) ||
                    (_attachedToMultiShopController && !_attachedToMultiShopController.available) ||
                    (_attachedToDroneVendorMultiShopController && !_attachedToDroneVendorMultiShopController.available))
                {
                    foreach (ParticleSystem particleSystem in ParticleSystems)
                    {
                        if (!particleSystem.isStopped)
                        {
                            particleSystem.Stop();
                        }
                    }
                }
                else
                {
                    foreach (ParticleSystem particleSystem in ParticleSystems)
                    {
                        if (particleSystem.isStopped)
                        {
                            particleSystem.Play();
                        }
                    }
                }
            }
        }

        void onReset(bool hasEffectData)
        {
            setAttachedObject(hasEffectData && _effectComponent.effectData != null ? _effectComponent.effectData.ResolveNetworkedObjectReference() : null);
        }

        void setAttachedObject(GameObject attachToObject)
        {
            if (_attachedToObject == attachToObject)
                return;

            if (_attachedToObjectAttributes)
            {
                foreach (GameObject obj in ChildObjectsToDisable)
                {
                    _attachedToObjectAttributes.childObjectsToDisable.Remove(obj);
                }
            }

            _attachedToObject = attachToObject;
            _attachedToObjectAttributes = _attachedToObject ? _attachedToObject.GetComponent<SpecialObjectAttributes>() : null;
            _attachedToPurchaseInteraction = _attachedToObject ? _attachedToObject.GetComponent<PurchaseInteraction>() : null;
            _attachedToBarrelInteraction = _attachedToObject ? _attachedToObject.GetComponent<BarrelInteraction>() : null;
            _attachedToMultiShopController = _attachedToObject ? _attachedToObject.GetComponent<MultiShopController>() : null;
            _attachedToDroneVendorMultiShopController = _attachedToObject ? _attachedToObject.GetComponent<DroneVendorMultiShopController>() : null;
            _attachedToSpeedOnPickupBarrelInteraction = _attachedToObject ? _attachedToObject.GetComponent<SpeedOnPickupBarrelInteraction>() : null;

            if (_attachedToObjectAttributes)
            {
                _attachedToObjectAttributes.childObjectsToDisable.AddRange(ChildObjectsToDisable);
            }
        }
    }
}
