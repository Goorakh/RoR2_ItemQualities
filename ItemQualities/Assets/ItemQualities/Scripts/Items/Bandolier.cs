using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Skills;
using RoR2.UI;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class Bandolier
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;

            IL.RoR2.AmmoPickup.OnTriggerStay += AmmoPickup_OnTriggerStay;

            IL.RoR2.UI.SkillIcon.Update += SkillIcon_Update;
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
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.Bandolier)),
                               x => x.MatchCallOrCallvirt(typeof(NetworkServer), nameof(NetworkServer.Spawn))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before);
            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldarg, damageReportParameter);
            c.EmitDelegate<Action<GameObject, DamageReport>>(beforeBandolierSpawn);

            static void beforeBandolierSpawn(GameObject bandolierObj, DamageReport damageReport)
            {
                if (!bandolierObj || damageReport == null)
                    return;

                CharacterMaster attackerMaster = damageReport.attackerMaster;
                Inventory attackerInventory = attackerMaster ? attackerMaster.inventory : null;

                ItemQualityCounts bandolier = default;
                if (attackerInventory)
                {
                    bandolier = ItemQualitiesContent.ItemQualityGroups.Bandolier.GetItemCounts(attackerInventory);
                }

                float extraSkillRestockChance = (10f * bandolier.UncommonCount) +
                                                (25f * bandolier.RareCount) +
                                                (50f * bandolier.EpicCount) +
                                                (100f * bandolier.LegendaryCount);

                int extraSkillRestocks = (int)(extraSkillRestockChance / 100f);

                if (Util.CheckRoll(extraSkillRestockChance % 100f, attackerMaster))
                {
                    extraSkillRestocks++;
                }

                if (extraSkillRestocks > 0 && bandolierObj.TryGetComponent(out BandolierQualityInfo bandolierQualityInfo))
                {
                    bandolierQualityInfo.ExtraSkillChargesServer = extraSkillRestocks;
                }
            }
        }

        static void AmmoPickup_OnTriggerStay(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!il.Method.TryFindParameter<Collider>(out ParameterDefinition otherColliderParameter))
            {
                Log.Error("Failed to find Collider parameter");
                return;
            }

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<SkillLocator>(nameof(SkillLocator.ApplyAmmoPack))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, otherColliderParameter);
            c.EmitDelegate<Action<AmmoPickup, Collider>>(onApplyAmmoPackServer);

            static void onApplyAmmoPackServer(AmmoPickup ammoPickup, Collider recipient)
            {
                if (!ammoPickup || !recipient)
                    return;

                BandolierQualityInfo bandolierQualityInfo = ammoPickup.GetComponentInParent<BandolierQualityInfo>();
                if (bandolierQualityInfo)
                {
                    bandolierQualityInfo.OnApplyAmmoPackServer(recipient.gameObject);
                }
            }
        }

        static void SkillIcon_Update(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<GenericSkill>("get_" + nameof(GenericSkill.maxStock))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<int, SkillIcon, int>>(getCurrentMaxStock);

            static int getCurrentMaxStock(int maxStock, SkillIcon skillIcon)
            {
                if (skillIcon && skillIcon.targetSkill)
                {
                    maxStock = Mathf.Max(skillIcon.targetSkill.stock, maxStock);
                }

                return maxStock;
            }
        }
    }
}
