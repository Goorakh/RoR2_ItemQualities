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
                ItemQualitiesContent.BuffQualityGroups.AttackSpeedOnCrit.EnsureBuffQualities(Body, QualityTier.None);
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            ItemQualitiesContent.BuffQualityGroups.AttackSpeedOnCrit.EnsureBuffQualities(Body, Stacks.HighestQuality);
        }
    }
}
