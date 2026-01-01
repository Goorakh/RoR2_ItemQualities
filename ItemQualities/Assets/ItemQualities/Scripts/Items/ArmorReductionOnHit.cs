using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class ArmorReductionOnHit
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.HealthComponent.TakeDamageProcess += QualityArmorReductionOnHit;
        }

        static void QualityArmorReductionOnHit(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.ArmorReductionOnHit)),
                               x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.ClearTimedBuffs))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After);

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate<Action<HealthComponent, DamageInfo>>(doExtraDamage);
        }

        static void doExtraDamage(HealthComponent victim, DamageInfo damageInfo)
        {
            CharacterBody victimBody = victim.body;
            if (!victimBody)
                return;

            CharacterBody attacker = damageInfo?.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
            if (!attacker)
                return;

            Inventory attackerInventory = attacker.inventory;
            if (!attackerInventory)
                return;

            ItemQualityCounts armorReductionOnHit = attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ArmorReductionOnHit);
            if (armorReductionOnHit.TotalQualityCount > 0)
            {
                float damageMultiplyer = 1.0f + (0.20f * armorReductionOnHit.UncommonCount) +
                                                (0.40f * armorReductionOnHit.RareCount) +
                                                (0.60f * armorReductionOnHit.EpicCount) +
                                                (1.00f * armorReductionOnHit.LegendaryCount);

                DamageInfo newDamage = new DamageInfo
                {
                    attacker = damageInfo.attacker,
                    damage = attacker.damage * damageMultiplyer,
                    crit = damageInfo.crit,
                    procCoefficient = 0f,
                    procChainMask = damageInfo.procChainMask,
                    position = damageInfo.position,
                    damageColorIndex = DamageColorIndex.Item
                };

                victim.TakeDamage(newDamage);
            }
        }
    }
}
