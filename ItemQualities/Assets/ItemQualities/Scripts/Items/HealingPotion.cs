using ItemQualities.Utilities;
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

            int potionItemTransformationVarIndex = -1;
            Instruction skipPotionTransformationTargetInstruction = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.healingPotion))) ||
                !c.TryGotoNext(MoveType.Before,
                               x => x.MatchLdloca(typeof(Inventory.ItemTransformation), il, out potionItemTransformationVarIndex),
                               x => x.MatchLdarg(0),
                               x => x.MatchLdfld<HealthComponent>(nameof(HealthComponent.body)),
                               x => x.MatchCallOrCallvirt<CharacterBody>("get_" + nameof(CharacterBody.inventory)),
                               x => x.MatchLdloca(typeof(Inventory.ItemTransformation.TryTransformResult), il, out _),
                               x => x.MatchCallOrCallvirt<Inventory.ItemTransformation>(nameof(Inventory.ItemTransformation.TryTransform)),
                               x => x.MatchBrfalse(out _),
                               x => x.MatchAny(out skipPotionTransformationTargetInstruction)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            ILLabel skipPotionTransformationLabel = c.DefineLabel();
            skipPotionTransformationLabel.Target = skipPotionTransformationTargetInstruction;

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<HealthComponent, bool>>(tryProtectElixirs);
            c.Emit(OpCodes.Brtrue, skipPotionTransformationLabel);

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloca, potionItemTransformationVarIndex);
            c.EmitDelegate<TryConsumeQualityElixirsDelegate>(tryConsumeQualityElixirs);

            static void tryConsumeQualityElixirs(HealthComponent healthComponent, ref Inventory.ItemTransformation itemTransformation)
            {
                CharacterBody body = healthComponent ? healthComponent.body : null;
                Inventory inventory = body ? body.inventory : null;
                if (!inventory)
                    return;

                ItemQualityCounts elixir = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.HealingPotion);

                if (elixir.BaseItemCount == 0 && elixir.TotalQualityCount > 0)
                {
                    for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        if (elixir[qualityTier] > 0)
                        {
                            itemTransformation.originalItemIndex = ItemQualitiesContent.ItemQualityGroups.HealingPotion.GetItemIndex(qualityTier);
                            itemTransformation.newItemIndex = ItemQualitiesContent.ItemQualityGroups.HealingPotionConsumed.GetItemIndex(qualityTier);
                            break;
                        }
                    }
                }
            }

            static bool tryProtectElixirs(HealthComponent healthComponent)
            {
                CharacterBody body = healthComponent ? healthComponent.body : null;
                CharacterMaster master = body ? body.master : null;
                Inventory inventory = body ? body.inventory : null;
                if (!inventory)
                    return false;

                ItemQualityCounts elixir = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.HealingPotion);

                for (int i = 0; i < elixir.UncommonCount; i++)
                {
                    if (RollUtil.CheckRoll(20f, master, false))
                    {
                        return true;
                    }
                }

                for (int i = 0; i < elixir.RareCount; i++)
                {
                    if (RollUtil.CheckRoll(35f, master, false))
                    {
                        return true;
                    }
                }

                for (int i = 0; i < elixir.EpicCount; i++)
                {
                    if (RollUtil.CheckRoll(50f, master, false))
                    {
                        return true;
                    }
                }

                for (int i = 0; i < elixir.LegendaryCount; i++)
                {
                    if (RollUtil.CheckRoll(75f, master, false))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        delegate void TryConsumeQualityElixirsDelegate(HealthComponent healthComponent, ref Inventory.ItemTransformation itemTransformation);
    }
}
