using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;

namespace ItemQualities.Items
{
    static class FragileDamageBonus
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;

            IL.RoR2.HealthComponent.UpdateLastHitTime += HealthComponent_UpdateLastHitTime;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            ItemQualityCounts fragileDamageBonus = ItemQualitiesContent.ItemQualityGroups.FragileDamageBonus.GetItemCountsEffective(sender.inventory);
            if (fragileDamageBonus.TotalQualityCount > 0)
            {
                BuffQualityCounts fragileDamageBonusBuff = ItemQualitiesContent.BuffQualityGroups.FragileDamageBonusBuff.GetBuffCounts(sender);
                if (fragileDamageBonusBuff.TotalQualityCount > 0)
                {
                    float damageBonusPerBuff = (0.05f * fragileDamageBonus.UncommonCount) +
                                               (0.10f * fragileDamageBonus.RareCount) +
                                               (0.15f * fragileDamageBonus.EpicCount) +
                                               (0.20f * fragileDamageBonus.LegendaryCount);

                    args.damageMultAdd += damageBonusPerBuff;
                }
            }
        }

        static void HealthComponent_UpdateLastHitTime(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int watchItemTransformationVarIndex = -1;
            int watchItemTransformationResultVarIndex = -1;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.fragileDamageBonus))) ||
                !c.TryGotoNext(MoveType.After,
                               x => x.MatchLdloca(typeof(Inventory.ItemTransformation), il, out watchItemTransformationVarIndex),
                               x => x.MatchLdarg(0),
                               x => x.MatchLdfld<HealthComponent>(nameof(HealthComponent.body)),
                               x => x.MatchCallOrCallvirt<CharacterBody>("get_" + nameof(CharacterBody.inventory)),
                               x => x.MatchLdloca(typeof(Inventory.ItemTransformation.TryTransformResult), il, out watchItemTransformationResultVarIndex),
                               x => x.MatchCallOrCallvirt<Inventory.ItemTransformation>(nameof(Inventory.ItemTransformation.TryTransform))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloca, watchItemTransformationVarIndex);
            c.Emit(OpCodes.Ldloca, watchItemTransformationResultVarIndex);
            c.EmitDelegate<ConsumeQualityWatchesDelegate>(consumeQualityWatches);

            static bool consumeQualityWatches(bool result, HealthComponent healthComponent, in Inventory.ItemTransformation itemTransformation, ref Inventory.ItemTransformation.TryTransformResult consumeTransformResult)
            {
                CharacterBody body = healthComponent ? healthComponent.body : null;
                Inventory inventory = body ? body.inventory : null;

                if (inventory)
                {
                    for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        Inventory.ItemTransformation qualityItemTransformation = itemTransformation;
                        qualityItemTransformation.originalItemIndex = ItemQualitiesContent.ItemQualityGroups.FragileDamageBonus.GetItemIndex(qualityTier);
                        qualityItemTransformation.newItemIndex = ItemQualitiesContent.ItemQualityGroups.FragileDamageBonusConsumed.GetItemIndex(qualityTier);

                        if (qualityItemTransformation.TryTransform(inventory, out Inventory.ItemTransformation.TryTransformResult qualityWatchConsumeTransformationResult))
                        {
                            result = true;

                            static void addStackValues(ref Inventory.ItemStackValues a, in Inventory.ItemStackValues b)
                            {
                                a.permanentStacks += b.permanentStacks;
                                a.temporaryStacksValue += b.temporaryStacksValue;
                                a.totalStacks += b.totalStacks;
                            }

                            addStackValues(ref consumeTransformResult.takenItem.stackValues, qualityWatchConsumeTransformationResult.takenItem.stackValues);
                            addStackValues(ref consumeTransformResult.givenItem.stackValues, qualityWatchConsumeTransformationResult.givenItem.stackValues);
                        }
                    }
                }

                return result;
            }
        }

        delegate bool ConsumeQualityWatchesDelegate(bool result, HealthComponent healthComponent, in Inventory.ItemTransformation itemTransformation, ref Inventory.ItemTransformation.TryTransformResult consumeTransformResult);
    }
}
