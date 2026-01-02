using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Orbs;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class ChainLightningVoid
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;
        }

        static void GlobalEventManager_ProcessHitEnemy(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.ChainLightningVoid)),
                               x => x.MatchCallOrCallvirt<OrbManager>(nameof(OrbManager.AddOrb))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before); // call OrbManager.AddOrb

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldarg, damageInfoParameter);

            c.EmitDelegate<Action<VoidLightningOrb, DamageInfo>>(tryHandleQualityOrb);

            static void tryHandleQualityOrb(VoidLightningOrb voidLightningOrb, DamageInfo damageInfo)
            {
                if (voidLightningOrb == null)
                    return;

                CharacterBody attackerBody = damageInfo?.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;
                if (!attackerInventory)
                    return;

                ItemQualityCounts chainLightningVoid = attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ChainLightningVoid);
                if (chainLightningVoid.TotalQualityCount > 0)
                {
                    float procCoefficientIncrease = (0.1f * chainLightningVoid.UncommonCount) +
                                                    (0.3f * chainLightningVoid.RareCount) +
                                                    (0.5f * chainLightningVoid.EpicCount) +
                                                    (0.8f * chainLightningVoid.LegendaryCount);

                    const float MaxProcCoefficient = 1f;

                    float procCoefficientToAdd = Mathf.Min(MaxProcCoefficient - voidLightningOrb.procCoefficient, procCoefficientIncrease);
                    voidLightningOrb.procCoefficient += procCoefficientToAdd;

                    procCoefficientIncrease -= procCoefficientToAdd;
                    if (procCoefficientIncrease > 0f)
                    {
                        float damageBonusCoefficient = procCoefficientIncrease * 0.5f;

                        voidLightningOrb.damageValue += damageInfo.damage * damageBonusCoefficient;
                    }
                }
            }
        }
    }
}
