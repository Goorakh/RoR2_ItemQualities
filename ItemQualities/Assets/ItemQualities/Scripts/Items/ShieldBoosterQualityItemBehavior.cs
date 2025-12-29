using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class ShieldBoosterQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.ShieldBooster;
        }

        CharacterBodyExtraStatsTracker _bodyExtraStats;

        float _boosterFraction;

        protected override void Awake()
        {
            base.Awake();

            _bodyExtraStats = this.GetComponentCached<CharacterBodyExtraStatsTracker>();
        }

        void OnEnable()
        {
            _bodyExtraStats.OnTakeDamageServer += onTakeDamageServer;
            ShieldBooster.OnShieldBoosterBreakServerGlobal += onShieldBoosterBreakServerGlobal;

            _boosterFraction = 0f;
            updateBuffCount();
        }

        void OnDisable()
        {
            _bodyExtraStats.OnTakeDamageServer -= onTakeDamageServer;
            ShieldBooster.OnShieldBoosterBreakServerGlobal -= onShieldBoosterBreakServerGlobal;

            Body.RemoveAllQualityBuffs(ItemQualitiesContent.BuffQualityGroups.ShieldBoosterBuff);
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            updateBuffCount();
        }

        void onTakeDamageServer(DamageReport damageReport)
        {
            if (damageReport.damageDealt > 0f && Body.healthComponent.shield > 0f)
            {
                float damageFractionMultiplier;
                switch (Stacks.HighestQuality)
                {
                    default:
                    case QualityTier.Uncommon:
                        damageFractionMultiplier = 0.75f;
                        break;
                    case QualityTier.Rare:
                        damageFractionMultiplier = 1f;
                        break;
                    case QualityTier.Epic:
                        damageFractionMultiplier = 1.5f;
                        break;
                    case QualityTier.Legendary:
                        damageFractionMultiplier = 2f;
                        break;
                }

                float boosterFractionIncrease = Mathf.Min(1f - _boosterFraction, damageFractionMultiplier * (damageReport.damageDealt / Body.healthComponent.fullCombinedHealth));
                if (boosterFractionIncrease > 0f)
                {
                    _boosterFraction += boosterFractionIncrease;
                    updateBuffCount();
                }
            }
        }

        void onShieldBoosterBreakServerGlobal(CharacterBody body)
        {
            if (body == Body)
            {
                _boosterFraction = 0f;
                updateBuffCount();
            }
        }

        void updateBuffCount()
        {
            ItemQualityCounts shieldBooster = Stacks;

            int currentBuffCount = Body.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.ShieldBoosterBuff).TotalQualityCount;
            int targetBuffCount = Mathf.CeilToInt(_boosterFraction * 100f);

            if (targetBuffCount != currentBuffCount)
            {
                BuffIndex shieldBoosterBuffIndex = ItemQualitiesContent.BuffQualityGroups.ShieldBoosterBuff.GetBuffIndex(shieldBooster.HighestQuality);
                if (targetBuffCount < currentBuffCount)
                {
                    for (int i = currentBuffCount; i > targetBuffCount; i--)
                    {
                        Body.RemoveBuff(shieldBoosterBuffIndex);
                    }
                }
                else
                {
                    for (int i = currentBuffCount; i < targetBuffCount; i++)
                    {
                        Body.AddBuff(shieldBoosterBuffIndex);
                    }
                }
            }

            Body.ConvertQualityBuffsToTier(ItemQualitiesContent.BuffQualityGroups.ShieldBoosterBuff, shieldBooster.HighestQuality);
        }
    }
}
