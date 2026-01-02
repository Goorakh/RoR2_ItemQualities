using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class KillEliteFrenzyQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.KillEliteFrenzy;
        }

        void OnDisable()
        {
            if (NetworkServer.active)
            {
                Body.RemoveAllQualityBuffs(ItemQualitiesContent.BuffQualityGroups.KillEliteFrenzyBuff);
            }
        }

        void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                if (!Body.HasBuff(RoR2Content.Buffs.NoCooldowns) &&
                    Body.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.KillEliteFrenzyBuff).TotalQualityCount > 0)
                {
                    Body.RemoveAllQualityBuffs(ItemQualitiesContent.BuffQualityGroups.KillEliteFrenzyBuff);
                }
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            Body.ConvertQualityBuffsToTier(ItemQualitiesContent.BuffQualityGroups.KillEliteFrenzyBuff, Stacks.HighestQuality);
        }
    }
}
