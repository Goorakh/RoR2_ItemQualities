using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class DronesDropDynamiteDroneItemQualityItemBehavior : MonoBehaviour
    {
        NetworkIdentity _netIdentity;

        CharacterBody _body;

        GameObject _attachmentInstance;

        void Awake()
        {
            _netIdentity = GetComponent<NetworkIdentity>();
            _body = GetComponent<CharacterBody>();
        }

        void OnEnable()
        {
            trySpawnAttachment();
        }

        void Start()
        {
            trySpawnAttachment();
        }

        void trySpawnAttachment()
        {
            if (!_attachmentInstance && !_netIdentity.netId.IsEmpty())
            {
                _attachmentInstance = Instantiate(ItemQualitiesContent.NetworkedPrefabs.DroneShootableAttachment);
                _attachmentInstance.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(gameObject);
            }
        }

        void OnDisable()
        {
            if (_attachmentInstance)
            {
                Destroy(_attachmentInstance);
                _attachmentInstance = null;
            }
        }
    }
}
