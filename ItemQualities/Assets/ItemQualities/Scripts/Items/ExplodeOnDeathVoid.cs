using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class ExplodeOnDeathVoid
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.ExplodeOnDeathVoid)),
                               x => x.MatchStfld<DelayBlast>(nameof(DelayBlast.baseDamage))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before); // stfld DelayBlast.baseDamage

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Func<float, DamageInfo, float>>(getBlastDamage);

            static float getBlastDamage(float damage, DamageInfo damageInfo)
            {
                CharacterBody attackerBody = damageInfo?.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                ItemQualityCounts explodeOnDeathVoid = ItemQualitiesContent.ItemQualityGroups.ExplodeOnDeathVoid.GetItemCountsEffective(attackerInventory);
                if (explodeOnDeathVoid.TotalQualityCount > 0)
                {
                    float bonusDamageCoefficient = (0.5f * explodeOnDeathVoid.UncommonCount) +
                                                   (0.7f * explodeOnDeathVoid.RareCount) +
                                                   (1.0f * explodeOnDeathVoid.EpicCount) +
                                                   (1.5f * explodeOnDeathVoid.LegendaryCount);

                    damage += damageInfo.damage * bonusDamageCoefficient;
                }

                return damage;
            }
        }
    }
}
