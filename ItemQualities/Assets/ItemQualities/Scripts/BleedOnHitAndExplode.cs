using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Projectile;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ItemQualities.Items
{
    public static class BleedOnHitAndExplode
    {
        static EquipmentIndex[] _bleedEquipments = Array.Empty<EquipmentIndex>();

        [SystemInitializer(typeof(QualityCatalog))]
        static void Init() 
        {
            HashSet<EquipmentIndex> bleedEquipments = new HashSet<EquipmentIndex>(EquipmentCatalog.equipmentCount*(int)QualityTier.Count);
            for (QualityTier qualityTier = QualityTier.None; qualityTier < QualityTier.Count; qualityTier++)
            {
                void tryAddEquipment(EquipmentQualityGroup equipmentGroup)
                {
                    if (!equipmentGroup)
                        return;

                    EquipmentIndex equipmentIndex = equipmentGroup.GetEquipmentIndex(qualityTier);
                    Debug.Log(qualityTier);
                    if (equipmentIndex != EquipmentIndex.None)
                    {
                        bleedEquipments.Add(equipmentIndex);
                    }
                }
                tryAddEquipment(ItemQualitiesContent.EquipmentQualityGroups.Saw);
            }

            if (bleedEquipments.Count > 0)
            {
                _bleedEquipments = bleedEquipments.ToArray();
                Array.Sort(_bleedEquipments);
            }

            On.RoR2.GlobalEventManager.ProcessHitEnemy += ProcessHitEnemy;
            On.RoR2.DotController.InflictDot_refInflictDotInfo += DotController_InflictDot_refInflictDotInfo;
        }

        private static void DotController_InflictDot_refInflictDotInfo(On.RoR2.DotController.orig_InflictDot_refInflictDotInfo orig, ref InflictDotInfo inflictDotInfo)
        {
            try
            {
                if (inflictDotInfo.dotIndex == DotController.DotIndex.SuperBleed)
                {
                    CharacterBody attackerBody = inflictDotInfo.attackerObject ? inflictDotInfo.attackerObject.GetComponent<CharacterBody>() : null;
                    Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;
                    if (attackerInventory && attackerBody.master)
                    {
                        ItemQualityCounts bleedOnHitAndExplode = attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BleedOnHitAndExplode);

                        float damageMult =  bleedOnHitAndExplode.UncommonCount * 0.03f +
                                            bleedOnHitAndExplode.RareCount * 0.06f +
                                            bleedOnHitAndExplode.EpicCount * 0.1f +
                                            bleedOnHitAndExplode.LegendaryCount * 0.15f;

                        inflictDotInfo.damageMultiplier += ((1 + damageMult) * getBleedCount(attackerBody.master));
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e.ToString());
            }

            orig(ref inflictDotInfo);
        }

        static void ProcessHitEnemy(On.RoR2.GlobalEventManager.orig_ProcessHitEnemy orig, GlobalEventManager self, DamageInfo damageInfo, GameObject victim)
        {
            orig(self, damageInfo, victim);
            if (!damageInfo.attacker)
                return;
            if (!damageInfo.attacker.TryGetComponent(out CharacterBody body) || !body.inventory || !body.master)
                return;
            if (!damageInfo.crit)
                return;

            ItemQualityCounts bleedOnHitAndExplode = body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BleedOnHitAndExplode);
            if (bleedOnHitAndExplode.TotalQualityCount == 0)
                return;

            uint? maxStacksFromAttacker = null;
            if (damageInfo?.inflictor)
            {
                ProjectileDamage projectileDamage = damageInfo.inflictor.GetComponent<ProjectileDamage>();
                if (projectileDamage && projectileDamage.useDotMaxStacksFromAttacker)
                {
                    maxStacksFromAttacker = projectileDamage.dotMaxStacksFromAttacker;
                }
            }

            float hemorrhageChance = bleedOnHitAndExplode.HighestQuality switch
            {
                QualityTier.Uncommon => 5f,
                QualityTier.Rare => 10f,
                QualityTier.Epic => 15f,
                QualityTier.Legendary => 20f,
                _ => 0
            };

            if (RollUtil.CheckRoll(hemorrhageChance, body.master, damageInfo.procChainMask.HasProc(ProcType.SureProc)))
            {
                DotController.InflictDot(victim, damageInfo.attacker, damageInfo.inflictedHurtbox, DotController.DotIndex.SuperBleed, 15f * damageInfo.procCoefficient, 1f, maxStacksFromAttacker);
            }
        }

        static int getBleedCount(CharacterMaster master)
        {
            int bleedItemCount = 0;

            if (!master.inventory.inventoryDisabled)
            {
                foreach (ItemIndex itemIndex in ItemCatalog.GetItemsWithTag(ItemTags.BleedRelated))
                {
                    bleedItemCount += master.inventory.GetItemCountEffective(itemIndex);
                }
            }

            if (!master.inventory.GetEquipmentDisabled())
            {
                int equipmentSlotCount = master.inventory.GetEquipmentSlotCount();
                for (uint slot = 0; slot < equipmentSlotCount; slot++)
                {
                    int equipmentSetCount = master.inventory.GetEquipmentSetCount(slot);
                    for (uint set = 0; set < equipmentSetCount; set++)
                    {
                        EquipmentState equipmentState = master.inventory.GetEquipment(slot, set);
                        if (equipmentState.equipmentIndex != EquipmentIndex.None &&
                            Array.BinarySearch(_bleedEquipments, equipmentState.equipmentIndex) >= 0)
                        {
                            bleedItemCount++;
                        }
                    }
                }
            }

            if (master && master.minionOwnership.ownerMaster)
            {
                bleedItemCount += getBleedCount(master.minionOwnership.ownerMaster);
            }

            return bleedItemCount;
        }
    }
}
