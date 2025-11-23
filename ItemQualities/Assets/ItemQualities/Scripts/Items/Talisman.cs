using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class Talisman
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
        }

        static void GlobalEventManager_OnCharacterDeath(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageReport>(out ParameterDefinition damageReportParameter))
            {
                Log.Error("Failed to find DamageReport parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.Talisman)),
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.DeductActiveEquipmentCooldown))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before); // call Inventory.DeductActiveEquipmentCooldown

            c.Emit(OpCodes.Ldarg, damageReportParameter);
            c.EmitDelegate<Func<float, DamageReport, float>>(getEquipmentCooldownReduction);

            static float getEquipmentCooldownReduction(float cooldownReduction, DamageReport damageReport)
            {
                Inventory attackerInventory = damageReport?.attackerBody ? damageReport.attackerBody.inventory : null;

                if (attackerInventory && attackerInventory.currentEquipmentIndex != EquipmentIndex.None)
                {
                    EquipmentDef currentEquipmentDef = EquipmentCatalog.GetEquipmentDef(attackerInventory.currentEquipmentIndex);
                    if (currentEquipmentDef)
                    {
                        float currentEquipmentCooldown = currentEquipmentDef.cooldown * attackerInventory.CalculateEquipmentCooldownScale();

                        ItemQualityCounts talisman = ItemQualitiesContent.ItemQualityGroups.Talisman.GetItemCountsEffective(attackerInventory);
                        if (talisman.TotalQualityCount > 0)
                        {
                            float cooldownReductionFraction = 0f;

                            if (damageReport.victimIsElite || damageReport.victimIsChampion)
                            {
                                cooldownReductionFraction += (0.05f * talisman.UncommonCount) +
                                                             (0.10f * talisman.RareCount) +
                                                             (0.20f * talisman.EpicCount) +
                                                             (0.33f * talisman.LegendaryCount);
                            }

                            if (cooldownReductionFraction > 0f)
                            {
                                cooldownReduction += cooldownReductionFraction * currentEquipmentCooldown;
                            }
                        }
                    }
                }

                return cooldownReduction;
            }
        }
    }
}
