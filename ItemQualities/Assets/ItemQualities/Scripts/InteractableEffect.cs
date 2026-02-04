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

                if (_attachedToPurchaseInteraction && !_attachedToPurchaseInteraction.available)
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

            if (_attachedToObjectAttributes)
            {
                _attachedToObjectAttributes.childObjectsToDisable.AddRange(ChildObjectsToDisable);
            }
        }
    }
}
