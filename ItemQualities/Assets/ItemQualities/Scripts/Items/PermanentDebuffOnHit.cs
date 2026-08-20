using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace ItemQualities.Items
{
    internal static class PermanentDebuffOnHit
    {
        private static DotController.DotIndex _scorpionVenomDot;

        [SystemInitializer]
        private static void Init()
        {
            DotController.DotDef dotDef = new DotController.DotDef
            {
                associatedBuff = ItemQualitiesContent.Buffs.ScorpionVenom,
                damageCoefficient = 0.5f,
                interval = 0.5f,
                damageColorIndex = DamageColorIndex.Item,
            };

            _scorpionVenomDot = DotAPI.RegisterDotDef(dotDef, null, null, dealVenomDamage);
            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        private static void onServerDamageDealt(DamageReport damageReport)
        {
            CharacterMaster attackerMaster = damageReport.attackerBody ? damageReport.attackerBody.master : null;
            Inventory attackerInventory = damageReport.attackerBody ? damageReport.attackerBody.inventory : null;
            if (!attackerInventory)
                return;

            ItemQualityCounts permanentDebuffOnHit = attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.PermanentDebuffOnHit);
            if (permanentDebuffOnHit.TotalQualityCount <= 0)
                return;

            uint? maxStacksFromAttacker = null;
            if (damageReport.damageInfo?.inflictor)
            {
                ProjectileDamage component = damageReport.damageInfo.inflictor.GetComponent<ProjectileDamage>();
                if ((bool)component && component.useDotMaxStacksFromAttacker)
                {
                    maxStacksFromAttacker = component.dotMaxStacksFromAttacker;
                }
            }

            float venomChance = permanentDebuffOnHit.HighestQuality switch
            {
                QualityTier.Uncommon => 5f,
                QualityTier.Rare => 8f,
                QualityTier.Epic => 12f,
                QualityTier.Legendary => 15f,
                _ => 0
            };

            if (RollUtil.CheckRoll(venomChance, attackerMaster, damageReport.damageInfo.procChainMask.HasProc(ProcType.SureProc)))
            {
                DotController.InflictDot(damageReport.victim.gameObject, damageReport.attacker, damageReport.damageInfo.inflictedHurtbox, _scorpionVenomDot, 10f * damageReport.damageInfo.procCoefficient, 1f, maxStacksFromAttacker);
            }
        }

        private static void dealVenomDamage(DotController self, DotController.PendingDamage pendingDamage)
        {
            GameObject attacker = pendingDamage.attackerObject;
            CharacterBody attackerBody = attacker ? attacker.GetComponent<CharacterBody>() : null;
            Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;
            if (!attackerInventory)
                return;

            ItemQualityCounts permanentDebuffOnHit = attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.PermanentDebuffOnHit);
            if (permanentDebuffOnHit.TotalQualityCount <= 0)
                return;

            float damageCoefficient = (permanentDebuffOnHit.UncommonCount * 0.5f) +
                                      (permanentDebuffOnHit.RareCount * 1f) +
                                      (permanentDebuffOnHit.EpicCount * 1.5f) +
                                      (permanentDebuffOnHit.LegendaryCount * 2f);

            int activeDots = 0;
            for (int i = 0; i < DotAPI.VanillaDotCount + DotAPI.CustomDotCount; i++)
            {
                if (self.HasDotActive((DotController.DotIndex)i))
                {
                    activeDots++;
                }
            }

            DamageInfo damageInfo = new DamageInfo();
            damageInfo.attacker = pendingDamage.attackerObject;
            damageInfo.crit = false;
            damageInfo.damage = pendingDamage.totalDamage * damageCoefficient * activeDots;
            damageInfo.force = Vector3.zero;
            damageInfo.inflictor = self.gameObject;
            damageInfo.position = self.victimBody.corePosition;
            damageInfo.procCoefficient = 0f;
            damageInfo.damageColorIndex = DotController.GetDotDef(_scorpionVenomDot).damageColorIndex;
            damageInfo.damageType = pendingDamage.damageType | DamageType.DoT;
            damageInfo.dotIndex = _scorpionVenomDot;
            damageInfo.inflictedHurtbox = pendingDamage.hitHurtBox;
            self.victimHealthComponent.TakeDamage(damageInfo);
        }
    }
}

