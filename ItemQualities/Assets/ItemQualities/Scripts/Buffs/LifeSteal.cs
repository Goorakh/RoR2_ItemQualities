using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;
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

            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;

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

        static void GlobalEventManager_ProcessHitEnemy(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition attackerBodyVar = null;
            ILLabel lifeStealEndLabel = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdloc<CharacterBody>(il, out attackerBodyVar),
                               x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.LifeSteal)),
                               x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.HasBuff)),
                               x => x.MatchBrfalse(out lifeStealEndLabel)))
            {
                Log.Error("Failed to find lifesteal buff check location");
                return;
            }

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdfld<DamageInfo>(nameof(DamageInfo.procChainMask)))
                || !c.IsBefore(lifeStealEndLabel.Target))
            {
                Log.Error("Failed to find heal proc patch location");
                return;
            }

            c.Emit(OpCodes.Ldloc, attackerBodyVar);
            c.EmitDelegate<Func<ProcChainMask, CharacterBody, ProcChainMask>>(getHealProcChainMask);

            static ProcChainMask getHealProcChainMask(ProcChainMask procChainMask, CharacterBody attackerBody)
            {
                BuffQualityCounts lifeSteal = attackerBody.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.LifeSteal);
                QualityTier lifeStealQuality = lifeSteal.HighestQuality;
                if (lifeStealQuality > QualityTier.None)
                {
                    procChainMask.AddModdedProc(ProcTypes.LifeStealOverhealProcTypes[(int)lifeStealQuality]);
                }

                return procChainMask;
            }
        }

        static void onCharacterHealServer(HealthComponent healthComponent, float amount, ProcChainMask procChainMask)
        {
            QualityTier lifeStealQualityTier = QualityTier.None;
            for (QualityTier qualityTier = QualityTier.Count - 1; qualityTier >= 0; qualityTier--)
            {
                if (procChainMask.HasModdedProc(ProcTypes.LifeStealOverhealProcTypes[(int)qualityTier]))
                {
                    lifeStealQualityTier = qualityTier;
                    break;
                }
            }

            if (lifeStealQualityTier == QualityTier.None)
                return;

            // How much healing is required to grant each speed buff, 1.0 is 100% hp healed
            const float HealFractionPerSpeedBuff = 0.08f;
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
