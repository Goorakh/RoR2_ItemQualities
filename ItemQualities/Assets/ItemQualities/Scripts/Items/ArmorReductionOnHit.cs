using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities
{
    public class ArmorReductionOnHit
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

        static void doExtraDamage(HealthComponent hc, DamageInfo di)
        {
            CharacterBody body = hc.body;
            if (!body)
                return;
            
            CharacterBody attacker = di?.attacker ? di.attacker.GetComponent<CharacterBody>() : null;
            if (attacker == null)
                return;

            Inventory attackerInventory = attacker.inventory;
            if (attackerInventory == null)
                return;

            ItemQualityCounts armorReductionOnHit = ItemQualitiesContent.ItemQualityGroups.ArmorReductionOnHit.GetItemCounts(attacker.inventory);

            if (armorReductionOnHit.TotalQualityCount < 1)
                return;
            
            float damageMultiplyer = 1.0f + (0.20f * armorReductionOnHit.UncommonCount) +
                                            (0.40f * armorReductionOnHit.RareCount) +
                                            (0.60f * armorReductionOnHit.EpicCount) +
                                            (1.00f * armorReductionOnHit.LegendaryCount);

            DamageInfo newDamage = new DamageInfo
            {
                attacker = attacker ? attacker.gameObject : null,
                damage = attacker.damage * damageMultiplyer,
                crit = di.crit,
                procCoefficient = 0f,
                procChainMask = di.procChainMask,
                position = di.position,
                damageColorIndex = DamageColorIndex.Item
            };

            hc.TakeDamage(newDamage);
        }
    }
}
