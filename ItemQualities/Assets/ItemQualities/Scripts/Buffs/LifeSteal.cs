using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;
using UnityEngine.Networking;

namespace ItemQualities.Buffs
{
    static class LifeSteal
    {
        [SystemInitializer]
        static void Init()
        {
            BuffHooks.OnBuffFirstStackGainedGlobal += onBuffFirstStackGainedGlobal;
            BuffHooks.OnBuffFinalStackLostGlobal += onBuffFinalStackLostGlobal;

            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;
            IL.RoR2.HealthComponent.Heal += HealthComponent_Heal;
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

        static void HealthComponent_Heal(ILContext il)
        {
            if (!il.Method.TryFindParameter<ProcChainMask>(out ParameterDefinition procChainMaskParameter))
            {
                Log.Error("Failed to find ProcChainMask parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            ILLabel barrierOnOverhealEndLabel = null;
            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchLdarg(0),
                               x => x.MatchLdflda<HealthComponent>(nameof(HealthComponent.itemCounts)),
                               x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(HealthComponent.itemCounts.barrierOnOverHeal)),
                               x => x.MatchLdcI4(0),
                               x => x.MatchBle(out barrierOnOverhealEndLabel)))
            {
                Log.Error("Failed to find aegis check location");
                return;
            }

            VariableDefinition overhealAmountVar = null;
            if (!c.TryGotoPrev(MoveType.Before,
                               x => x.MatchLdloc<float>(il, out overhealAmountVar),
                               x => x.MatchLdcR4(0),
                               x => x.MatchBleUn(out ILLabel label) && barrierOnOverhealEndLabel.Target == label.Target))
            {
                Log.Error("Failed to find overheal check location");
                return;
            }

            c.Goto(barrierOnOverhealEndLabel.Target, MoveType.AfterLabel);

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, procChainMaskParameter);
            c.Emit(OpCodes.Ldloc, overhealAmountVar);
            c.EmitDelegate<Action<HealthComponent, ProcChainMask, float>>(handleOverheal);

            static void handleOverheal(HealthComponent healthComponent, ProcChainMask procChainMask, float overhealAmount)
            {
                if (!healthComponent || overhealAmount <= 0)
                    return;

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

                float barrierConversionRate;
                switch (lifeStealQualityTier)
                {
                    case QualityTier.Uncommon:
                        barrierConversionRate = 0.8f;
                        break;
                    case QualityTier.Rare:
                        barrierConversionRate = 1.0f;
                        break;
                    case QualityTier.Epic:
                        barrierConversionRate = 1.4f;
                        break;
                    case QualityTier.Legendary:
                        barrierConversionRate = 2.0f;
                        break;
                    default:
                        barrierConversionRate = 0f;
                        Log.Warning($"Quality tier {lifeStealQualityTier} is not implemented");
                        break;
                }

                float barrierAmount = overhealAmount * barrierConversionRate;
                if (barrierAmount > 0f)
                {
                    healthComponent.AddBarrier(barrierAmount);
                }
            }
        }
    }
}
