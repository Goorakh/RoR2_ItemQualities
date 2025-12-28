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
                ItemQualitiesContent.BuffQualityGroups.ArmorPlateBuildup.EnsureBuffQualities(Body, QualityTier.None);
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            QualityTier buffQualityTier = Stacks.HighestQuality;
            ItemQualitiesContent.BuffQualityGroups.ArmorPlateBuildup.EnsureBuffQualities(Body, buffQualityTier);

            if (buffQualityTier > QualityTier.None)
            {
                ItemQualitiesContent.BuffQualityGroups.ArmorPlateBuff.EnsureBuffQualities(Body, buffQualityTier);
            }
        }
    }
}
