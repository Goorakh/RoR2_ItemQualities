using RoR2;
using System;
using UnityEngine;

namespace ItemQualities
{
    [RequireComponent(typeof(EffectComponent))]
    public sealed class InteractableAttachmentEffect : MonoBehaviour
    {
        [Tooltip("Added to SpecialObjectAttributes.childObjectsToDisable of the attached interactable object.")]
        public GameObject[] ChildObjectsToDisable = Array.Empty<GameObject>();

        EffectComponent _effectComponent;

        GameObject _attachedToObject;

        SpecialObjectAttributes _attachedToObjectAttributes;

        void Awake()
        {
            _effectComponent = GetComponent<EffectComponent>();
            _effectComponent.OnEffectComponentReset += onReset;
        }

        void OnDisable()
        {
            setAttachedObject(null);
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

            if (_attachedToObjectAttributes)
            {
                _attachedToObjectAttributes.childObjectsToDisable.AddRange(ChildObjectsToDisable);
            }
        }
    }
}
