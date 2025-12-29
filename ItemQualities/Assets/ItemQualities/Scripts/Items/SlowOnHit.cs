using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class SlowOnHit
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;
        }

        static void GlobalEventManager_ProcessHitEnemy(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            if (!il.Method.TryFindParameter<GameObject>("victim", out ParameterDefinition victimParameter))
            {
                Log.Error("Failed to find victim parameter");
                return;
            }

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.SlowOnHit)),
                               x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.AddTimedBuff))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before);

            VariableDefinition isQualityProcVar = il.AddVariable<bool>();

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.Emit(OpCodes.Ldarg, victimParameter);
            c.Emit(OpCodes.Ldloca, isQualityProcVar);
            c.EmitDelegate<ShouldApplyBaseSlowOnHitDelegate>(shouldApplyBaseSlowOnHit);
            c.EmitSkipMethodCall(OpCodes.Brfalse);

            static bool shouldApplyBaseSlowOnHit(DamageInfo damageInfo, GameObject victim, out bool isQualityProc)
            {
                GameObject attacker = damageInfo?.attacker;
                CharacterBody attackerBody = attacker ? attacker.GetComponent<CharacterBody>() : null;
                CharacterMaster attackerMaster = attackerBody ? attackerBody.master : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;
                if (!attackerInventory)
                {
                    isQualityProc = false;
                    return true;
                }

                ItemQualityCounts slowOnHit = attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SlowOnHit);
                if (slowOnHit.TotalQualityCount <= 0)
                {
                    isQualityProc = false;
                    return true;
                }

                float qualitySlowOnHitChance = (10f * slowOnHit.UncommonCount) +
                                               (20f * slowOnHit.RareCount) +
                                               (35f * slowOnHit.EpicCount) +
                                               (60f * slowOnHit.LegendaryCount);

                isQualityProc = RollUtil.CheckRoll(Util.ConvertAmplificationPercentageIntoReductionPercentage(qualitySlowOnHitChance) * damageInfo.procCoefficient, attackerMaster, damageInfo.procChainMask.HasProc(ProcType.SureProc));

                if (isQualityProc)
                    return false;

                CharacterBody victimBody = victim ? victim.GetComponent<CharacterBody>() : null;
                return victimBody && victimBody.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.Slow60).TotalQualityCount == 0;
            }

            c.Emit(OpCodes.Ldloc, isQualityProcVar);
            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.Emit(OpCodes.Ldarg, victimParameter);
            c.EmitDelegate<Action<bool, DamageInfo, GameObject>>(handleQualityProc);

            static void handleQualityProc(bool isQualityProc, DamageInfo damageInfo, GameObject victim)
            {
                if (!isQualityProc)
                    return;

                CharacterBody victimBody = victim ? victim.GetComponent<CharacterBody>() : null;
                if (!victimBody)
                    return;

                GameObject attacker = damageInfo?.attacker;
                CharacterBody attackerBody = attacker ? attacker.GetComponent<CharacterBody>() : null;
                CharacterMaster attackerMaster = attackerBody ? attackerBody.master : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;
                if (!attackerInventory)
                    return;

                ItemQualityCounts slowOnHit = attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SlowOnHit);

                QualityTier slowOnHitQuality = slowOnHit.HighestQuality;
                if (slowOnHitQuality == QualityTier.None)
                    return;

                for (QualityTier lowerQualityTier = slowOnHitQuality - 1; lowerQualityTier >= QualityTier.None; lowerQualityTier--)
                {
                    BuffIndex lowerQualitySlowDebuffIndex = ItemQualitiesContent.BuffQualityGroups.Slow60.GetBuffIndex(lowerQualityTier);
                    if (victimBody.HasBuff(lowerQualitySlowDebuffIndex))
                    {
                        victimBody.ClearTimedBuffs(lowerQualitySlowDebuffIndex);
                    }
                }

                BuffIndex slowDebuffIndex = ItemQualitiesContent.BuffQualityGroups.Slow60.GetBuffIndex(slowOnHitQuality);
                victimBody.AddTimedBuff(slowDebuffIndex, 2f * slowOnHit.TotalCount);
            }
        }

        delegate bool ShouldApplyBaseSlowOnHitDelegate(DamageInfo damageInfo, GameObject victim, out bool isQualityProc);
    }
}
