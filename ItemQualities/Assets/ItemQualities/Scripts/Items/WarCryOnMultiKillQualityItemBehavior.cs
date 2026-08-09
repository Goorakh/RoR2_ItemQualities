using ItemQualities.Utilities.Extensions;
using RoR2;

namespace ItemQualities.Items
{
    public sealed class WarCryOnMultiKillQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.WarCryOnMultiKill;
        }

        private bool _hadWarCryBuff;

        private bool hasWarCryBuff => Body.HasBuff(RoR2Content.Buffs.WarCryBuff) || Body.HasBuff(RoR2Content.Buffs.TeamWarCry);

        private void OnEnable()
        {
            BodyStats.OnKilledOther += onKilledOther;

            _hadWarCryBuff = false;
        }

        private void OnDisable()
        {
            BodyStats.OnKilledOther -= onKilledOther;

            setWarCryBuffCount(0);
        }

        private void FixedUpdate()
        {
            bool hasBuff = hasWarCryBuff;
            if (hasBuff != _hadWarCryBuff)
            {
                setWarCryBuffCount(hasBuff ? BodyStats.EliteKillCount : 0);
                _hadWarCryBuff = hasBuff;
            }
        }

        private void onKilledOther(DamageReport damageReport)
        {
            if (damageReport.victimIsElite)
            {
                if (hasWarCryBuff)
                {
                    setWarCryBuffCount(BodyStats.EliteKillCount);
                }
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            setWarCryBuffCount(hasWarCryBuff ? BodyStats.EliteKillCount : 0);
        }

        private void setWarCryBuffCount(int count)
        {
            int currentBuffCount = Body.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.MultikillWarCryBuff).TotalQualityCount;
            if (currentBuffCount != count)
            {
                BuffIndex qualityBuffIndex = ItemQualitiesContent.BuffQualityGroups.MultikillWarCryBuff.GetBuffIndex(Stacks.HighestQuality);

                if (currentBuffCount < count)
                {
                    for (int i = currentBuffCount; i < count; i++)
                    {
                        Body.AddBuff(qualityBuffIndex);
                    }
                }
                else
                {
                    for (int i = currentBuffCount; i > count; i--)
                    {
                        Body.RemoveBuff(qualityBuffIndex);
                    }
                }

                Body.ConvertQualityBuffsToTier(ItemQualitiesContent.BuffQualityGroups.MultikillWarCryBuff, Stacks.HighestQuality);
            }
        }
    }
}
