using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Buffs
{
    static class LifeSteal
    {
        public const float LifeStealSpeedDuration = 60f;

        [SystemInitializer]
        static void Init()
        {
            BuffHooks.OnBuffFirstStackGainedGlobal += onBuffFirstStackGainedGlobal;
            BuffHooks.OnBuffFinalStackLostGlobal += onBuffFinalStackLostGlobal;

            HealthComponent.onCharacterHealServer += onCharacterHealServer;
        }

        static void onBuffFirstStackGainedGlobal(CharacterBody body, BuffDef buffDef)
        {
            if (!NetworkServer.active)
                return;

            BuffIndex buffIndex = buffDef ? buffDef.buffIndex : BuffIndex.None;
            QualityTier qualityTier = QualityCatalog.GetQualityTier(buffIndex);
            BuffQualityGroupIndex buffGroupIndex = QualityCatalog.FindBuffQualityGroupIndex(buffIndex);

            if (buffGroupIndex != ItemQualitiesContent.BuffQualityGroups.LifeSteal.GroupIndex)
                return;

            for (QualityTier buffQualityTier = QualityTier.None; buffQualityTier < QualityTier.Count; buffQualityTier++)
            {
                BuffIndex qualityBuffIndex = QualityCatalog.GetBuffIndexOfQuality(buffIndex, buffQualityTier);
                if (qualityBuffIndex != buffIndex && body.HasBuffRaw(qualityBuffIndex))
                {
                    if (buffQualityTier > qualityTier)
                    {
                        body.ClearTimedBuffsRaw(buffIndex);
                        return;
                    }

                    body.ClearTimedBuffsRaw(qualityBuffIndex);
                }
            }

            body.ClearTimedBuffs(ItemQualitiesContent.Buffs.LifeStealSpeed);
        }
        
        static void onBuffFinalStackLostGlobal(CharacterBody body, BuffDef buffDef)
        {
            if (!NetworkServer.active)
                return;

            BuffIndex buffIndex = buffDef ? buffDef.buffIndex : BuffIndex.None;
            QualityTier qualityTier = QualityCatalog.GetQualityTier(buffIndex);
            BuffQualityGroupIndex buffGroupIndex = QualityCatalog.FindBuffQualityGroupIndex(buffIndex);

            if (buffGroupIndex != ItemQualitiesContent.BuffQualityGroups.LifeSteal.GroupIndex || qualityTier == QualityTier.None)
                return;

            for (QualityTier buffQualityTier = 0; buffQualityTier < QualityTier.Count; buffQualityTier++)
            {
                BuffIndex qualityBuffIndex = QualityCatalog.GetBuffIndexOfQuality(buffIndex, buffQualityTier);
                if (qualityBuffIndex != buffIndex && body.HasBuffRaw(qualityBuffIndex))
                {
                    return;
                }
            }

            foreach (CharacterBody.TimedBuff timedBuff in body.timedBuffs)
            {
                if (timedBuff.buffIndex == ItemQualitiesContent.Buffs.LifeStealSpeed.buffIndex)
                {
                    timedBuff.timer = Mathf.Max(timedBuff.timer, LifeStealSpeedDuration);
                }
            }

            if (body.TryGetComponentCached(out CharacterBodyExtraStatsTracker bodyExtraStats))
            {
                bodyExtraStats.LeechBuffReserveFraction = 0f;
            }
        }

        static void onCharacterHealServer(HealthComponent healthComponent, float amount, ProcChainMask procChainMask)
        {
            BuffQualityCounts lifeSteal = healthComponent.body.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.LifeSteal);

            QualityTier lifeStealQualityTier = lifeSteal.HighestQuality;
            if (lifeStealQualityTier == QualityTier.None)
                return;

            // How much healing is required to grant each speed buff, 1.0 is 100% hp healed
            const float HealFractionPerSpeedBuff = 0.05f;
            const float SpeedBuffsPerFullHeal = 1f / HealFractionPerSpeedBuff;

            int maxSpeedBuffCount;
            switch (lifeStealQualityTier)
            {
                case QualityTier.Uncommon:
                    maxSpeedBuffCount = 50;
                    break;
                case QualityTier.Rare:
                    maxSpeedBuffCount = 100;
                    break;
                case QualityTier.Epic:
                    maxSpeedBuffCount = 150;
                    break;
                case QualityTier.Legendary:
                    maxSpeedBuffCount = 250;
                    break;
                default:
                    Log.Warning($"Quality tier {lifeStealQualityTier} is not implemented");
                    maxSpeedBuffCount = 0;
                    break;
            }

            if (healthComponent.TryGetComponentCached(out CharacterBodyExtraStatsTracker bodyExtraStats))
            {
                int currentSpeedBuffCount = healthComponent.body.GetBuffCount(ItemQualitiesContent.Buffs.LifeStealSpeed);

                int maxBuffsToAdd = Mathf.Max(0, maxSpeedBuffCount - currentSpeedBuffCount);

                float healFraction = amount / healthComponent.fullHealth;

                float buffsToAdd = Mathf.Min(maxBuffsToAdd, bodyExtraStats.LeechBuffReserveFraction + (SpeedBuffsPerFullHeal * healFraction));

                foreach (CharacterBody.TimedBuff timedBuff in healthComponent.body.timedBuffs)
                {
                    if (timedBuff.buffIndex == ItemQualitiesContent.Buffs.LifeStealSpeed.buffIndex)
                    {
                        timedBuff.timer = Mathf.Max(timedBuff.timer, LifeStealSpeedDuration);
                    }
                }

                for (int i = 0; i < (int)buffsToAdd; i++)
                {
                    healthComponent.body.AddTimedBuff(ItemQualitiesContent.Buffs.LifeStealSpeed, LifeStealSpeedDuration);
                }

                // Add fractional component into reserve so healing is never wasted when it doesnt reach a full buff stack at once
                bodyExtraStats.LeechBuffReserveFraction = buffsToAdd - (int)buffsToAdd;
            }
        }
    }
}
