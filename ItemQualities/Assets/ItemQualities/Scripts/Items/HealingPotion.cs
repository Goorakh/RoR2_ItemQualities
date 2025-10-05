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
                               x => x.MatchCallOrCallvirt(typeof(HealthComponent).GetProperty(nameof(HealthComponent.isHealthLow)).GetMethod),
                               x => x.MatchBrfalse(out _)))
            {
                Log.Error("Failed to find healing potion break location");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.After);

            Instruction elixirBlockStartInstruction = c.Next;

            ILLabel afterBaseElixirLogicLabel = c.DefineLabel();

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<HealthComponent, bool>>(shouldBreakBaseElixirs);
            c.Emit(OpCodes.Brfalse, afterBaseElixirLogicLabel);

            static bool shouldBreakBaseElixirs(HealthComponent healthComponent)
            {
                CharacterBody body = healthComponent ? healthComponent.body : null;
                CharacterMaster master = body ? body.master : null;
                Inventory inventory = body ? body.inventory : null;

                ItemQualityCounts elixir = default;
                if (inventory)
                {
                    elixir = ItemQualitiesContent.ItemQualityGroups.HealingPotion.GetItemCounts(inventory);
                }

                float elixirProtectChance = (25f * elixir.UncommonCount) +  // 20%
                                            (55f * elixir.RareCount) +      // 35%
                                            (100f * elixir.EpicCount) +     // 50%
                                            (300f * elixir.LegendaryCount); // 75%

                if (Util.CheckRoll(Util.ConvertAmplificationPercentageIntoReductionPercentage(elixirProtectChance), master))
                    return false;

                bool qualityElixirBroken = false;

                if (elixir.BaseItemCount == 0 && elixir.TotalQualityCount > 0)
                {
                    for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        ItemIndex elixirItemIndex = ItemQualitiesContent.ItemQualityGroups.HealingPotion.GetItemIndex(qualityTier);
                        ItemIndex consumedElixirItemIndex = ItemQualitiesContent.ItemQualityGroups.HealingPotionConsumed.GetItemIndex(qualityTier);

                        int elixirCount = elixir[qualityTier];
                        if (elixirCount > 0)
                        {
                            inventory.RemoveItem(elixirItemIndex, 1);
                            inventory.GiveItem(consumedElixirItemIndex, 1);

                            CharacterMasterNotificationQueue.SendTransformNotification(body.master, elixirItemIndex, consumedElixirItemIndex, CharacterMasterNotificationQueue.TransformationType.Default);

                            qualityElixirBroken = true;
                            break;
                        }
                    }
                }

                if (qualityElixirBroken)
                    return false;

                return true;
            }

            c.Goto(elixirBlockStartInstruction.Next);
            if (c.TryGotoNext(MoveType.After,
                              x => x.MatchCallOrCallvirt<CharacterMasterNotificationQueue>(nameof(CharacterMasterNotificationQueue.SendTransformNotification))))
            {
                c.MarkLabel(afterBaseElixirLogicLabel);
            }
            else
            {
                Log.Error($"Failed to find quality elixir break patch location");
            }
        }
    }
}
