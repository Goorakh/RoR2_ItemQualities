using ItemQualities.Utilities.Extensions;
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
                Body.RemoveAllQualityBuffs(ItemQualitiesContent.BuffQualityGroups.KillMoveSpeed);
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            Body.ConvertQualityBuffsToTier(ItemQualitiesContent.BuffQualityGroups.KillMoveSpeed, Stacks.HighestQuality);
        }
    }
}
