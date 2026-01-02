using RoR2;
using RoR2.Items;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class DronesDropDynamiteDroneItemQualityItemBehavior : BaseItemBodyBehavior
    {
        [ItemDefAssociation(useOnServer = true)]
        static ItemDef GetItemDef()
        {
            return ItemQualitiesContent.Items.DronesDropDynamiteQualityDroneItem;
        }

        GameObject _attachmentInstance;

        void OnEnable()
        {
            _attachmentInstance = Instantiate(ItemQualitiesContent.NetworkedPrefabs.DroneShootableAttachment);
            _attachmentInstance.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(gameObject);
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
