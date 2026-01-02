using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class DuplicatorQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.Duplicator;
        }

        GameObject _attachmentInstance;

        void OnEnable()
        {
            if (!Body.master || !Body.master.minionOwnership.ownerMaster)
            {
                _attachmentInstance = Instantiate(ItemQualitiesContent.NetworkedPrefabs.DuplicatorQualityAttachment);
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
