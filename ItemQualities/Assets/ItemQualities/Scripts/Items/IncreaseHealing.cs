using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class IncreaseHealing
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.HealthComponent.Heal += HealthComponent_Heal;
        }

        static void HealthComponent_Heal(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.increaseHealing)),
                               x => x.MatchStarg(out _)))
            {
                Log.Error("Failed to find IncreaseHealing item location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before); // starg amount

            if (!c.TryGotoPrev(MoveType.Before,
                               x => x.MatchMul()))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, HealthComponent, float>>(getHealingIncrease);

            static float getHealingIncrease(float healingIncrease, HealthComponent healthComponent)
            {
                if (healthComponent && healthComponent.body)
                {
                    ItemQualityCounts increaseHealing = ItemQualitiesContent.ItemQualityGroups.IncreaseHealing.GetItemCountsEffective(healthComponent.body.inventory);
                    if (increaseHealing.TotalQualityCount > 0)
                    {
                        healingIncrease += ((1.5f - 1f) * increaseHealing.UncommonCount) +
                                           ((2.0f - 1f) * increaseHealing.RareCount) +
                                           ((3.0f - 1f) * increaseHealing.EpicCount) +
                                           ((4.0f - 1f) * increaseHealing.LegendaryCount);
                    }
                }

                return healingIncrease;
            }
        }
    }
}
