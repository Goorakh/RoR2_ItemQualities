using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class Behemoth
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.OnHitAllProcess += GlobalEventManager_OnHitAllProcess;
        }

        static void GlobalEventManager_OnHitAllProcess(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.Behemoth)),
                               x => x.MatchCallOrCallvirt<BlastAttack>(nameof(BlastAttack.Fire))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before); // call BlastAttack.Fire

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Action<BlastAttack, DamageInfo>>(handleQualityBehemoth);

            static void handleQualityBehemoth(BlastAttack behemothBlastAttack, DamageInfo damageInfo)
            {
                if (behemothBlastAttack == null || !damageInfo?.attacker)
                    return;

                CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;
                if (!attackerInventory)
                    return;

                ItemQualityCounts behemoth = attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Behemoth);
                if (behemoth.TotalQualityCount > 0)
                {
                    float procCoefficient = (0.1f * behemoth.UncommonCount) +
                                            (0.3f * behemoth.RareCount) +
                                            (0.7f * behemoth.EpicCount) +
                                            (0.9f * behemoth.LegendaryCount);

                    const float MaxProcCoefficient = 1f;

                    if (procCoefficient > 0f)
                    {
                        float blastAttackProcCoefficientAdd = Mathf.Min(MaxProcCoefficient - behemothBlastAttack.procCoefficient, procCoefficient);
                        behemothBlastAttack.procCoefficient += blastAttackProcCoefficientAdd;
                        behemothBlastAttack.procChainMask.AddProc(ProcType.Behemoth);

                        procCoefficient -= blastAttackProcCoefficientAdd;

                        if (procCoefficient > 0f)
                        {
                            float bonusDamageCoefficient = procCoefficient * 0.5f;

                            behemothBlastAttack.baseDamage += damageInfo.damage * bonusDamageCoefficient;
                        }
                    }
                }
            }
        }
    }
}
