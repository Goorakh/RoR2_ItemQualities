using RoR2;
using UnityEngine;

namespace ItemQualities
{
    [RequireComponent(typeof(NetworkedBodyAttachment))]
    public class PositionIndicatorAttachment : MonoBehaviour, INetworkedBodyAttachmentListener
    {
        public PositionIndicator PositionIndicator;

        void INetworkedBodyAttachmentListener.OnAttachedBodyDiscovered(NetworkedBodyAttachment networkedBodyAttachment, CharacterBody attachedBody)
        {
            if (PositionIndicator)
            {
                PositionIndicator.targetTransform = attachedBody.coreTransform;
            }
        }
    }
}
