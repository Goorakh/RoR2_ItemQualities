using ItemQualities.Utilities.Extensions;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class ArmorPlateQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.ArmorPlate;
        }

        void OnDisable()
        {
            if (NetworkServer.active)
            {
                Body.RemoveAllQualityBuffs(ItemQualitiesContent.BuffQualityGroups.ArmorPlateBuildup);
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            Body.ConvertQualityBuffsToTier(ItemQualitiesContent.BuffQualityGroups.ArmorPlateBuildup, Stacks.HighestQuality);
            Body.ConvertQualityBuffsToTier(ItemQualitiesContent.BuffQualityGroups.ArmorPlateBuff, Stacks.HighestQuality);
        }
    }
}
