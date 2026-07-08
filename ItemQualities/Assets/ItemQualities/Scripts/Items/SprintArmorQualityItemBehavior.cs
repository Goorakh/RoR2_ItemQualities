using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class SprintArmorQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.SprintArmor;
        }

        private GameObject _sprintArmorDashAttachment;

        private void OnEnable()
        {
            _sprintArmorDashAttachment = Instantiate(ItemQualitiesContent.NetworkedPrefabs.SprintArmorDashAttachment);
            NetworkedBodyAttachment sprintArmorDashAttachment = _sprintArmorDashAttachment.GetComponent<NetworkedBodyAttachment>();
            sprintArmorDashAttachment.AttachToGameObjectAndSpawn(Body.gameObject);
        }

        private void OnDisable()
        {
            Destroy(_sprintArmorDashAttachment);

            Body.ClearTimedBuffs(ItemQualitiesContent.Buffs.SprintArmorDashCooldown);
        }
    }
}
