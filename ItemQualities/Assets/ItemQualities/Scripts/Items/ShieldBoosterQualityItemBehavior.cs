using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class ShieldBoosterQualityItemBehavior : MonoBehaviour
    {
        CharacterBody _body;
        CharacterBodyExtraStatsTracker _bodyExtraStats;

        float _boosterFraction;

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
            _bodyExtraStats = GetComponent<CharacterBodyExtraStatsTracker>();
        }

        void OnEnable()
        {
            _body.onInventoryChanged += onInventoryChanged;
            _bodyExtraStats.OnTakeDamageServer += onTakeDamageServer;
            ShieldBooster.OnShieldBoosterBreakServerGlobal += onShieldBoosterBreakServerGlobal;

            _boosterFraction = 0f;
            updateBuffCount();
        }

        void OnDisable()
        {
            _body.onInventoryChanged -= onInventoryChanged;
            _bodyExtraStats.OnTakeDamageServer -= onTakeDamageServer;
            ShieldBooster.OnShieldBoosterBreakServerGlobal -= onShieldBoosterBreakServerGlobal;

            ItemQualitiesContent.BuffQualityGroups.ShieldBoosterBuff.EnsureBuffQualities(_body, QualityTier.None);
        }

        void onInventoryChanged()
        {
            updateBuffCount();
        }

        void onTakeDamageServer(DamageReport damageReport)
        {
            if (damageReport.damageDealt > 0f && _body.healthComponent.shield > 0f)
            {
                float damageFractionMultiplier;
                switch (ItemQualitiesContent.ItemQualityGroups.ShieldBooster.GetItemCountsEffective(_body.inventory).HighestQuality)
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

                float boosterFractionIncrease = Mathf.Min(1f - _boosterFraction, damageFractionMultiplier * (damageReport.damageDealt / _body.healthComponent.fullCombinedHealth));
                if (boosterFractionIncrease > 0f)
                {
                    _boosterFraction += boosterFractionIncrease;
                    updateBuffCount();
                }
            }
        }

        void onShieldBoosterBreakServerGlobal(CharacterBody body)
        {
            if (body == _body)
            {
                _boosterFraction = 0f;
                updateBuffCount();
            }
        }

        void updateBuffCount()
        {
            ItemQualityCounts shieldBooster = ItemQualitiesContent.ItemQualityGroups.ShieldBooster.GetItemCountsEffective(_body.inventory);

            int currentBuffCount = ItemQualitiesContent.BuffQualityGroups.ShieldBoosterBuff.GetBuffCounts(_body).TotalQualityCount;
            int targetBuffCount = Mathf.CeilToInt(_boosterFraction * 100f);

            if (targetBuffCount != currentBuffCount)
            {
                BuffIndex shieldBoosterBuffIndex = ItemQualitiesContent.BuffQualityGroups.ShieldBoosterBuff.GetBuffIndex(shieldBooster.HighestQuality);
                if (targetBuffCount < currentBuffCount)
                {
                    for (int i = currentBuffCount; i > targetBuffCount; i--)
                    {
                        _body.RemoveBuff(shieldBoosterBuffIndex);
                    }
                }
                else
                {
                    for (int i = currentBuffCount; i < targetBuffCount; i++)
                    {
                        _body.AddBuff(shieldBoosterBuffIndex);
                    }
                }
            }

            ItemQualitiesContent.BuffQualityGroups.ShieldBoosterBuff.EnsureBuffQualities(_body, shieldBooster.HighestQuality);
        }
    }
}
