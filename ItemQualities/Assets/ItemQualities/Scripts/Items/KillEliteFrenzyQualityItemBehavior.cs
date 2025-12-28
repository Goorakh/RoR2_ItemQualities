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
                ItemQualitiesContent.BuffQualityGroups.KillEliteFrenzyBuff.EnsureBuffQualities(Body, QualityTier.None);
            }
        }

        void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                if (!Body.HasBuff(RoR2Content.Buffs.NoCooldowns) &&
                    ItemQualitiesContent.BuffQualityGroups.KillEliteFrenzyBuff.GetBuffCounts(Body).TotalQualityCount > 0)
                {
                    ItemQualitiesContent.BuffQualityGroups.KillEliteFrenzyBuff.EnsureBuffQualities(Body, QualityTier.None);
                }
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            ItemQualitiesContent.BuffQualityGroups.KillEliteFrenzyBuff.EnsureBuffQualities(Body, Stacks.HighestQuality);
        }
    }
}
