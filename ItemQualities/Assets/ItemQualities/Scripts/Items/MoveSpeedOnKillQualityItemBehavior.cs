using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class MoveSpeedOnKillQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.MoveSpeedOnKill;
        }

        void OnDisable()
        {
            if (NetworkServer.active)
            {
                ItemQualitiesContent.BuffQualityGroups.KillMoveSpeed.EnsureBuffQualities(Body, QualityTier.None);
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            QualityTier buffQualityTier = Stacks.HighestQuality;
            ItemQualitiesContent.BuffQualityGroups.KillMoveSpeed.EnsureBuffQualities(Body, buffQualityTier);
        }
    }
}
