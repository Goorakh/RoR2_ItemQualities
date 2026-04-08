using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class SprintArmorQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.SprintArmor;
        }

        GameObject _sprintArmorDashAttachment;

        void OnEnable()
        {
            _sprintArmorDashAttachment = Instantiate(ItemQualitiesContent.NetworkedPrefabs.SprintArmorDashAttachment);
            NetworkedBodyAttachment sprintArmorDashAttachment = _sprintArmorDashAttachment.GetComponent<NetworkedBodyAttachment>();
            sprintArmorDashAttachment.AttachToGameObjectAndSpawn(Body.gameObject);
        }

        void OnDisable()
        {
            Destroy(_sprintArmorDashAttachment);

            Body.ClearTimedBuffs(ItemQualitiesContent.Buffs.SprintArmorDashCooldown);
        }
    }
}
