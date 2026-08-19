using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class NovaOnLowHealthQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.NovaOnLowHealth;

        private GameObject _attachmentInstance;

        private void OnEnable()
        {
            _attachmentInstance = Instantiate(ItemQualitiesContent.NetworkedPrefabs.VagrantNovaItemQualityAttachment, transform.position, transform.rotation);
            _attachmentInstance.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(gameObject);
        }

        private void FixedUpdate()
        {
            if (!Body.healthComponent.alive)
            {
                if (!ReferenceEquals(_attachmentInstance, null))
                {
                    Destroy(_attachmentInstance);
                    _attachmentInstance = null;
                }
            }
        }

        private void OnDisable()
        {
            if (!ReferenceEquals(_attachmentInstance, null))
            {
                Destroy(_attachmentInstance);
                _attachmentInstance = null;
            }
        }
    }
}
