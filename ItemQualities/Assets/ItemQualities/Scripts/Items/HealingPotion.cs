using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class HealingPotion
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.HealthComponent.UpdateLastHitTime += HealthComponent_UpdateLastHitTime;
        }

        static void HealthComponent_UpdateLastHitTime(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.healingPotion)),
                               x => x.MatchCallOrCallvirt<Inventory.ItemTransformation>(nameof(Inventory.ItemTransformation.TryTransform))))
            {
                Log.Error("Failed to find healing potion break location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After); // call Inventory.ItemTransformation.TryTransform

            ItemHooks.EmitCombinedQualityItemTransformationPatch(c);

            c.Goto(foundCursors[1].Next, MoveType.Before); // call Inventory.ItemTransformation.TryTransform

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<HealthComponent, bool>>(tryProtectElixirs);
            c.EmitSkipMethodCall(OpCodes.Brtrue, c =>
            {
                c.Emit(OpCodes.Ldc_I4_0); // false
            });

            static bool tryProtectElixirs(HealthComponent healthComponent)
            {
                CharacterBody body = healthComponent ? healthComponent.body : null;
                CharacterMaster master = body ? body.master : null;
                Inventory inventory = body ? body.inventory : null;

                ItemQualityCounts elixir = ItemQualitiesContent.ItemQualityGroups.HealingPotion.GetItemCountsEffective(inventory);
                if (elixir.TotalQualityCount <= 0)
                    return false;

                float elixirProtectChance = (25f * elixir.UncommonCount) +  // 20%
                                            (55f * elixir.RareCount) +      // 35%
                                            (100f * elixir.EpicCount) +     // 50%
                                            (300f * elixir.LegendaryCount); // 75%

                return Util.CheckRoll(Util.ConvertAmplificationPercentageIntoReductionPercentage(elixirProtectChance), master);
            }
        }
    }
}
