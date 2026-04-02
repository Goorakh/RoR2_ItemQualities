using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Equipments
{
    [RequireComponent(typeof(NetworkedBodyAttachment))]
    public sealed class GatewayQualityAttachment : NetworkBehaviour, INetworkedBodyAttachmentListener
    {
        NetworkedBodyAttachment _bodyAttachment;

        [NonSerialized]
        [SyncVar]
        public QualityTier QualityTier = QualityTier.None;

        CharacterBody _attachedBody;
        IPhysMotor _attachedBodyMotor;

        void Awake()
        {
            _bodyAttachment = GetComponent<NetworkedBodyAttachment>();
        }

        void OnEnable()
        {
            InstanceTracker.Add(this);
        }

        void OnDisable()
        {
            InstanceTracker.Remove(this);
        }

        void FixedUpdate()
        {

        }

        void INetworkedBodyAttachmentListener.OnAttachedBodyDiscovered(NetworkedBodyAttachment networkedBodyAttachment, CharacterBody attachedBody)
        {
            _attachedBody = attachedBody;
            _attachedBodyMotor = _attachedBody ? _attachedBody.GetComponent<IPhysMotor>() : null;
        }

        public static GatewayQualityAttachment FindAttachmentForBody(CharacterBody body)
        {
            foreach (GatewayQualityAttachment attachment in InstanceTracker.GetInstancesList<GatewayQualityAttachment>())
            {
                if (attachment._attachedBody == body)
                {
                    return attachment;
                }
            }

            return null;
        }
    }
}
