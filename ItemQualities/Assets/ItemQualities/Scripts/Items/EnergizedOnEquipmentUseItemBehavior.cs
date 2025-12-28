namespace ItemQualities.Items
{
    public sealed class EnergizedOnEquipmentUseItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.EnergizedOnEquipmentUse;
        }

        void OnDisable()
        {
            ItemQualitiesContent.BuffQualityGroups.Energized.EnsureBuffQualities(Body, QualityTier.None, true);
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            ItemQualitiesContent.BuffQualityGroups.Energized.EnsureBuffQualities(Body, Stacks.HighestQuality, true);
        }
    }
}
