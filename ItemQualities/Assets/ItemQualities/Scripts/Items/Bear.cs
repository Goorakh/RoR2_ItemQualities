using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class Bear
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
                               x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.bear)),
                               x => x.MatchStfld<DamageInfo>(nameof(DamageInfo.rejected))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After); // stfld DamageInfo.rejected

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Action<HealthComponent, DamageInfo>>(onBearBlockDamage);

            static void onBearBlockDamage(HealthComponent healthComponent, DamageInfo damageInfo)
            {
                CharacterBody body = healthComponent ? healthComponent.body : null;
                Inventory inventory = body ? body.inventory : null;
                if (!inventory)
                    return;

                ItemQualityCounts bear = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Bear);
                if (bear.TotalQualityCount > 0)
                {
                    float damageFraction = damageInfo.damage / healthComponent.fullCombinedHealth;

                    float invincibilityDurationPerPercentDamage = (0.01f * bear.UncommonCount) +
                                                                  (0.05f * bear.RareCount) +
                                                                  (0.15f * bear.EpicCount) +
                                                                  (0.25f * bear.LegendaryCount);

                    float invincibilityDuration = damageFraction * 100f * invincibilityDurationPerPercentDamage;
                    if (invincibilityDuration >= 1f / 30f)
                    {
                        body.AddTimedBuff(RoR2Content.Buffs.Immune, invincibilityDuration);
                    }
                }
            }
        }
    }
}
