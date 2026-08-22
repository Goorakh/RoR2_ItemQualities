using ItemQualities.Utilities.Extensions;
using RoR2;

namespace ItemQualities.Items
{
    public sealed class EnergizedOnEquipmentUseItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.EnergizedOnEquipmentUse;
        }

        private void OnDisable()
        {
            if (Body.inventory && Body.inventory.GetItemCountEffective(RoR2Content.Items.EnergizedOnEquipmentUse) > 0)
            {
                Body.ConvertAllBuffsToQualityTier(ItemQualitiesContent.BuffQualityGroups.Energized, QualityTier.None);
            }
            else
            {
                Body.RemoveAllQualityBuffs(ItemQualitiesContent.BuffQualityGroups.Energized);
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            Body.ConvertAllBuffsToQualityTier(ItemQualitiesContent.BuffQualityGroups.Energized, Stacks.HighestQuality);
        }
    }
}
