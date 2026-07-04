using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class Talisman
    {
        static Action<Inventory> _invokeInventoryOnEquipmentExternalRestockServer;

        [SystemInitializer]
        static void Init()
        {
            _invokeInventoryOnEquipmentExternalRestockServer = EventUtils.GetInvokeMethodDelegate<Action<Inventory>>(typeof(Inventory), nameof(Inventory.onEquipmentExternalRestockServer));

            IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
        }

        private static void onCharacterDeathGlobal(DamageReport damageReport)
        {
            if (!NetworkServer.active)
                return;

            Inventory attackerInventory = damageReport?.attackerBody ? damageReport.attackerBody.inventory : null;

            if (attackerInventory && attackerInventory.currentEquipmentIndex != EquipmentIndex.None)
            {
                ItemQualityCounts talisman = attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Talisman);

                if (talisman.TotalQualityCount > 0 && damageReport.victimIsChampion)
                {
                    int temporaryCharges = (talisman.UncommonCount * 1) +
                                           (talisman.RareCount * 2) +
                                           (talisman.EpicCount * 3) +
                                           (talisman.LegendaryCount * 4);

                    EquipmentState equipmentState = attackerInventory.GetEquipment(attackerInventory.activeEquipmentSlot, attackerInventory.activeEquipmentSet[attackerInventory.activeEquipmentSlot]);
                    equipmentState.charges = HGMath.ByteSafeAdd(equipmentState.charges, (byte)Math.Min(temporaryCharges, byte.MaxValue));

                    if (equipmentState.charges > attackerInventory.GetEquipmentSlotMaxCharges())
                    {
                        equipmentState.chargeFinishTime = Run.FixedTimeStamp.positiveInfinity;
                    }

                    attackerInventory.SetEquipment(equipmentState, attackerInventory.activeEquipmentSlot, attackerInventory.activeEquipmentSet[attackerInventory.activeEquipmentSlot]);
                    _invokeInventoryOnEquipmentExternalRestockServer?.Invoke(attackerInventory);
                }
            }
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

                        ItemQualityCounts talisman = attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Talisman);
                        if (talisman.TotalQualityCount > 0)
                        {
                            float cooldownReductionFraction = 0f;

                            if (damageReport.victimIsElite)
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
