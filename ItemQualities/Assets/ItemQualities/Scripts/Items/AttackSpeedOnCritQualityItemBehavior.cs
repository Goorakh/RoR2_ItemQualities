using ItemQualities.Utilities.Extensions;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class AttackSpeedOnCritQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.AttackSpeedOnCrit;
        }

        void OnDisable()
        {
            if (NetworkServer.active)
            {
                Body.RemoveAllQualityBuffs(ItemQualitiesContent.BuffQualityGroups.AttackSpeedOnCrit);
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            Body.ConvertQualityBuffsToTier(ItemQualitiesContent.BuffQualityGroups.AttackSpeedOnCrit, Stacks.HighestQuality);
        }
    }
}
