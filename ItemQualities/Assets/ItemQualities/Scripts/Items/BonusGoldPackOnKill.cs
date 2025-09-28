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
    static class BonusGoldPackOnKill
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
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.BonusGoldPackOnKill)),
                               x => x.MatchCallOrCallvirt(typeof(NetworkServer), nameof(NetworkServer.Spawn))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before);
            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldarg, damageReportParameter);
            c.EmitDelegate<Action<GameObject, DamageReport>>(beforeGoldPackSpawn);

            static void beforeGoldPackSpawn(GameObject goldPack, DamageReport damageReport)
            {
                if (!goldPack || damageReport == null)
                    return;

                CharacterMaster attackerMaster = damageReport.attackerMaster;
                Inventory attackerInventory = attackerMaster ? attackerMaster.inventory : null;

                ItemQualityCounts bonusGoldPackOnKill = default;
                if (attackerInventory)
                {
                    bonusGoldPackOnKill = ItemQualitiesContent.ItemQualityGroups.BonusGoldPackOnKill.GetItemCounts(attackerInventory);
                }

                float bigPackChance = (5f * bonusGoldPackOnKill.UncommonCount) +
                                      (15f * bonusGoldPackOnKill.RareCount) +
                                      (35f * bonusGoldPackOnKill.EpicCount) +
                                      (50f * bonusGoldPackOnKill.LegendaryCount);

                bool isBigPack = Util.CheckRoll(bigPackChance, attackerMaster);

                int bonusGoldReward = (10 * bonusGoldPackOnKill.UncommonCount) +
                                      (25 * bonusGoldPackOnKill.RareCount) +
                                      (75 * bonusGoldPackOnKill.EpicCount) +
                                      (150 * bonusGoldPackOnKill.LegendaryCount);

                MoneyPickup moneyPickup = goldPack.GetComponentInChildren<MoneyPickup>();
                if (moneyPickup)
                {
                    moneyPickup.baseGoldReward += bonusGoldReward;

                    if (isBigPack)
                    {
                        moneyPickup.baseGoldReward = Mathf.RoundToInt(moneyPickup.baseGoldReward * 1.5f);
                    }
                }

                if (isBigPack)
                {
                    goldPack.transform.localScale *= 1.5f;
                }
            }
        }
    }
}
