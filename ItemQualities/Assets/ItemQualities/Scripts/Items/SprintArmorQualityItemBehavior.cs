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

        GameObject _sprintArmorDashObj;

        private void OnEnable()
        {
            _sprintArmorDashObj = GameObject.Instantiate(ItemQualitiesContent.NetworkedPrefabs.SprintArmorDashAttachment);
            NetworkedBodyAttachment sprintArmorDashAttachment = _sprintArmorDashObj.GetComponent<NetworkedBodyAttachment>();
            sprintArmorDashAttachment.AttachToGameObjectAndSpawn(Body.gameObject);
        }

        private void OnDisable()
        {
            Object.Destroy(_sprintArmorDashObj);
        }
    }
}
