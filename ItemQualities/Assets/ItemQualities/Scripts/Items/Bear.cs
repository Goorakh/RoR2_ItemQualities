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
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.bear)),
                               x => x.MatchCallOrCallvirt(typeof(Util), nameof(Util.ConvertAmplificationPercentageIntoReductionPercentage))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, HealthComponent, float>>(getBearBlockChance);

            static float getBearBlockChance(float blockChance, HealthComponent healthComponent)
            {
                CharacterBody body = healthComponent ? healthComponent.body : null;
                Inventory inventory = body ? body.inventory : null;

                ItemQualityCounts bear = default;
                if (inventory)
                {
                    bear = ItemQualitiesContent.ItemQualityGroups.Bear.GetItemCounts(inventory);
                }

                blockChance += ((20f - 15f) * bear.UncommonCount) +
                               ((30f - 15f) * bear.RareCount) +
                               ((40f - 15f) * bear.EpicCount) +
                               ((50f - 15f) * bear.LegendaryCount);

                return blockChance;
            }
        }
    }
}
