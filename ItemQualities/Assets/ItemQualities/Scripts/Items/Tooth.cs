using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class Tooth
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
        }

        static void GlobalEventManager_OnCharacterDeath(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!il.Method.TryFindParameter<DamageReport>(out ParameterDefinition damageReportParameter))
            {
                Log.Error("Failed to find DamageReport parameter");
                return;
            }

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.Tooth)),
                               x => x.MatchCallOrCallvirt(typeof(NetworkServer), nameof(NetworkServer.Spawn))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before);
            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldarg, damageReportParameter);
            c.EmitDelegate<Action<GameObject, DamageReport>>(beforeHealingOrbSpawn);

            static void beforeHealingOrbSpawn(GameObject healingOrb, DamageReport damageReport)
            {
                if (!healingOrb || damageReport == null)
                    return;

                HealthPickup healthPickup = healingOrb.GetComponentInChildren<HealthPickup>();
                GravitatePickup gravitatePickup = healingOrb.GetComponentInChildren<GravitatePickup>();
                SphereCollider gravitatePickupTrigger = gravitatePickup ? gravitatePickup.GetComponent<SphereCollider>() : null;

                CharacterMaster attackerMaster = damageReport.attackerMaster;
                Inventory attackerInventory = attackerMaster ? attackerMaster.inventory : null;

                ItemQualityCounts tooth = default;
                if (attackerInventory)
                {
                    tooth = ItemQualitiesContent.ItemQualityGroups.Tooth.GetItemCounts(attackerInventory);
                }

                float healingMultiplier = 1f;
                healingMultiplier += 0.2f * tooth.UncommonCount;
                healingMultiplier += 0.4f * tooth.RareCount;
                healingMultiplier += 0.7f * tooth.EpicCount;
                healingMultiplier += 1.0f * tooth.LegendaryCount;

                float pickupRangeMultiplier = 1f;
                pickupRangeMultiplier += 0.2f * tooth.UncommonCount;
                pickupRangeMultiplier += 0.4f * tooth.RareCount;
                pickupRangeMultiplier += 0.7f * tooth.EpicCount;
                pickupRangeMultiplier += 1.0f * tooth.LegendaryCount;

                float bigOrbChance = (5f * tooth.UncommonCount) +
                                     (15f * tooth.RareCount) +
                                     (25f * tooth.EpicCount) +
                                     (50f * tooth.LegendaryCount);

                if (Util.CheckRoll(bigOrbChance, attackerMaster))
                {
                    healingMultiplier *= 2f;
                    pickupRangeMultiplier *= 1.5f;
                }

                if (healthPickup)
                {
                    healthPickup.fractionalHealing *= healingMultiplier;
                }

                if (healingMultiplier > 1f)
                {
                    healingOrb.transform.localScale *= Mathf.Pow(healingMultiplier, 0.5f);
                }

                if (gravitatePickupTrigger)
                {
                    // This probably doesn't need to be networked..?
                    // Even though the gravitate trigger is *technically* simulated clientside,
                    // the transform networker overrides the position so it probably doesn't matter in the end?
                    gravitatePickupTrigger.radius *= pickupRangeMultiplier;
                }
            }
        }
    }
}
