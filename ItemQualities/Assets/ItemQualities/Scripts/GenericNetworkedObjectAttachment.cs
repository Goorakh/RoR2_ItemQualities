using HG;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class GenericNetworkedObjectAttachment : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onSyncAttachedToObject))]
        GameObject _attachedToObject;

        [SyncVar(hook = nameof(onSyncAttachedObjectChildName))]
        string _attachedToObjectChildName;

        public bool ShouldParentToAttachedObject = true;

        public bool ForceHostAuthority;

        NetworkIdentity _networkIdentity;

        CharacterBody _attachmentBody;

        bool _attached;

        public GameObject AttachedToObject => _attachedToObject;

        public bool HasEffectiveAuthority { get; private set; }

        private void Awake()
        {
            _networkIdentity = GetComponent<NetworkIdentity>();
            _attachmentBody = GetComponent<CharacterBody>();
        }

        [Server]
        public void AttachToGameObjectAndSpawn(GameObject newAttachedObject, string attachedChildName = null)
        {
            if (_attached)
            {
                Log.Error($"Can't attach object '{gameObject}' to object '{newAttachedObject}', it's already been assigned to object '{AttachedToObject}'.");
                return;
            }

            if (!newAttachedObject)
            {
                return;
            }

            NetworkIdentity attachedObjectNetworkIdentity = newAttachedObject.GetComponent<NetworkIdentity>();
            if (attachedObjectNetworkIdentity.netId.Value == 0U)
            {
                Log.Warning($"Network Identity for object {newAttachedObject} has a zero netID. Attachment will fail over the network.");
            }

            _attachedToObjectChildName = attachedChildName;
            _attachedToObject = newAttachedObject;
            onAttachedObjectAssigned();

            NetworkConnection clientAuthorityOwner = attachedObjectNetworkIdentity.clientAuthorityOwner;
            if (clientAuthorityOwner == null || ForceHostAuthority)
            {
                NetworkServer.Spawn(gameObject);
            }
            else
            {
                NetworkServer.SpawnWithClientAuthority(gameObject, clientAuthorityOwner);
            }
        }

        void onAttachedObjectAssigned()
        {
            if (_attached)
            {
                return;
            }

            _attached = true;

            if (_attachedToObject)
            {
                if (ShouldParentToAttachedObject)
                {
                    parentToObject();
                }

                using var _ = ListPool<INetworkedObjectAttachmentListener>.RentCollection(out var attachmentListeners);
                GetComponents(attachmentListeners);

                foreach (INetworkedObjectAttachmentListener networkedBodyAttachmentListener in attachmentListeners)
                {
                    networkedBodyAttachmentListener.OnAttachedObjectDiscovered(this, _attachedToObject);
                }
            }
        }

        void parentToObject()
        {
            if (_attachedToObject)
            {
                Transform attachedToTransform = _attachedToObject.transform;
                if (!string.IsNullOrEmpty(_attachedToObjectChildName))
                {
                    if (_attachedToObject.TryGetComponent(out ModelLocator modelLocator))
                    {
                        ChildLocator modelChildLocator = modelLocator.modelChildLocator;
                        if (modelChildLocator)
                        {
                            if (modelChildLocator.TryFindChild(_attachedToObjectChildName, out Transform childTransform))
                            {
                                attachedToTransform = childTransform;
                            }
                            else if (modelChildLocator.TryFindChild("Root", out Transform rootTransform))
                            {
                                attachedToTransform = rootTransform;
                            }
                        }
                    }
                }

                transform.SetParent(attachedToTransform, false);
                transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
        }

        private void FixedUpdate()
        {
            if (!AttachedToObject && NetworkServer.active)
            {
                if (_attachmentBody && _attachmentBody.healthComponent)
                {
                    _attachmentBody.healthComponent.Suicide(null, null, default);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }

        private void OnValidate()
        {
            if (!GetComponent<NetworkIdentity>().localPlayerAuthority && !ForceHostAuthority)
            {
                Debug.LogWarningFormat("GenericNetworkedObjectAttachment: Object {0} NetworkIdentity needs localPlayerAuthority=true", new object[] { gameObject.name });
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            onSyncAttachedToObject(_attachedToObject);
        }

        public override void OnStartAuthority()
        {
            base.OnStartAuthority();
            HasEffectiveAuthority = Util.HasEffectiveAuthority(_networkIdentity);
        }

        public override void OnStopAuthority()
        {
            base.OnStopAuthority();
            HasEffectiveAuthority = Util.HasEffectiveAuthority(_networkIdentity);
        }

        void onSyncAttachedToObject(GameObject value)
        {
            if (NetworkServer.active)
            {
                return;
            }

            _attachedToObject = value;
            onAttachedObjectAssigned();
        }

        void onSyncAttachedObjectChildName(string newName)
        {
            _attachedToObjectChildName = newName;
            if (ShouldParentToAttachedObject)
            {
                parentToObject();
            }
        }
    }

    public interface INetworkedObjectAttachmentListener
    {
        void OnAttachedObjectDiscovered(GenericNetworkedObjectAttachment attachment, GameObject attachedObject);
    }
}
