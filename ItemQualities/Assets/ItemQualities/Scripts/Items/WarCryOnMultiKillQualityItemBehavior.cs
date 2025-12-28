using RoR2;

namespace ItemQualities.Items
{
    public sealed class WarCryOnMultiKillQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.WarCryOnMultiKill;
        }

        CharacterBodyExtraStatsTracker _bodyExtraStats;

        bool _hadWarCryBuff;

        bool hasWarCryBuff => Body.HasBuff(RoR2Content.Buffs.WarCryBuff) || Body.HasBuff(RoR2Content.Buffs.TeamWarCry);

        protected override void Awake()
        {
            base.Awake();

            _bodyExtraStats = GetComponent<CharacterBodyExtraStatsTracker>();
        }

        void OnEnable()
        {
            _bodyExtraStats.OnKilledOther += onKilledOther;

            _hadWarCryBuff = false;
        }

        void OnDisable()
        {
            _bodyExtraStats.OnKilledOther -= onKilledOther;

            setWarCryBuffCount(0);
        }

        void FixedUpdate()
        {
            bool hasBuff = hasWarCryBuff;
            if (hasBuff != _hadWarCryBuff)
            {
                setWarCryBuffCount(hasBuff ? _bodyExtraStats.EliteKillCount : 0);
                _hadWarCryBuff = hasBuff;
            }
        }

        void onKilledOther(DamageReport damageReport)
        {
            if (damageReport.victimIsElite)
            {
                if (hasWarCryBuff)
                {
                    setWarCryBuffCount(_bodyExtraStats.EliteKillCount);
                }
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            setWarCryBuffCount(hasWarCryBuff ? _bodyExtraStats.EliteKillCount : 0);
        }

        void setWarCryBuffCount(int count)
        {
            int currentBuffCount = ItemQualitiesContent.BuffQualityGroups.MultikillWarCryBuff.GetBuffCounts(Body).TotalQualityCount;
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

                updateBuffQualities();
            }
        }

        void updateBuffQualities()
        {
            ItemQualitiesContent.BuffQualityGroups.MultikillWarCryBuff.EnsureBuffQualities(Body, Stacks.HighestQuality);
        }
    }
}
