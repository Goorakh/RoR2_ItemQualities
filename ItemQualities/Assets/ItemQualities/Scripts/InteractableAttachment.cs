using RoR2;
using UnityEngine;

namespace ItemQualities
{
    [RequireComponent(typeof(GenericNetworkedObjectAttachment))]
    public sealed class InteractableAttachment : MonoBehaviour, INetworkedObjectAttachmentListener
    {
        private GameObject _lastAttachedObject;
        private SpecialObjectAttributes _attachedSpecialObjectAttributes;

        private void OnDestroy()
        {
            SetAttachedObject(null);
        }

        private void SetAttachedObject(GameObject newAttachedObject)
        {
            if (ReferenceEquals(newAttachedObject, _lastAttachedObject))
            {
                return;
            }

            if (!ReferenceEquals(_attachedSpecialObjectAttributes, null))
            {
                _attachedSpecialObjectAttributes.childObjectsToDisable.Remove(gameObject);
            }

            _lastAttachedObject = newAttachedObject;
            _attachedSpecialObjectAttributes = newAttachedObject ? newAttachedObject.GetComponent<SpecialObjectAttributes>() : null;

            if (!ReferenceEquals(_attachedSpecialObjectAttributes, null))
            {
                _attachedSpecialObjectAttributes.childObjectsToDisable.Add(gameObject);
            }
        }

        public void OnAttachedObjectDiscovered(GenericNetworkedObjectAttachment attachment, GameObject attachedObject)
        {
            SetAttachedObject(attachedObject);
        }
    }
}
