using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    internal static class FragileDamageBonus
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;

            IL.RoR2.HealthComponent.UpdateLastHitTime += HealthComponent_UpdateLastHitTime;
        }

        private static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.PatchError(il, "Failed to find DamageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!ItemHooks.TryGotoNextItemCountVariable(c, typeof(DLC1Content.Items), nameof(DLC1Content.Items.FragileDamageBonus), out VariableDefinition fragileDamageBonusCountVar))
            {
                Log.PatchError(il, "Failed to find FragileDamageBonus itemCount variable");
                return;
            }

            /*
             *  // 1f + (float)itemCountEffective5 * 0.2f
             *  IL_0CC7: ldc.r4    1
             *  IL_0CCC: ldloc.s   V_42
             *  IL_0CCE: conv.r4
             *  IL_0CCF: ldc.r4    0.2
             *  IL_0CD4: mul
             *  IL_0CD5: add
             */

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdcR4(out _),
                               x => x.MatchLdloc(fragileDamageBonusCountVar),
                               x => x.MatchConvR4(),
                               x => x.MatchLdcR4(out _),
                               x => x.MatchMul(),
                               x => x.MatchAdd()))
            {
                Log.PatchError(il, "Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg, damageInfoParameter);

            c.EmitDelegate<Func<float, DamageInfo, float>>(getWatchDamage);

            static float getWatchDamage(float fragileDamageBonusValue, DamageInfo damageInfo)
            {
                GameObject attacker = damageInfo?.attacker;
                CharacterBody attackerBody = attacker ? attacker.GetComponent<CharacterBody>() : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;
                TeamIndex attackerTeam = TeamComponent.GetObjectTeam(attacker);

                if (attackerInventory)
                {
                    ItemQualityCounts fragileDamageBonus = attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.FragileDamageBonus);
                    BuffQualityCounts fragileDamageBonusBuff = attackerBody.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.FragileDamageBonusBuff);
                    if (fragileDamageBonus.TotalQualityCount > 0 && fragileDamageBonusBuff.TotalQualityCount > 0)
                    {
                        float damageBonus = (0.10f * fragileDamageBonus.UncommonCount) +
                                            (0.20f * fragileDamageBonus.RareCount) +
                                            (0.40f * fragileDamageBonus.EpicCount) +
                                            (0.60f * fragileDamageBonus.LegendaryCount);

                        fragileDamageBonusValue += damageBonus;
                    }
                }

                return fragileDamageBonusValue;
            }
        }

        private static void HealthComponent_UpdateLastHitTime(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition watchItemTransformationVar = null;
            VariableDefinition watchItemTransformationResultVar = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.fragileDamageBonus))) ||
                !c.TryGotoNext(MoveType.After,
                               x => x.MatchLdloca(typeof(Inventory.ItemTransformation), il, out watchItemTransformationVar),
                               x => x.MatchLdarg(0),
                               x => x.MatchLdfld<HealthComponent>(nameof(HealthComponent.body)),
                               x => x.MatchCallOrCallvirt<CharacterBody>("get_" + nameof(CharacterBody.inventory)),
                               x => x.MatchLdloca(typeof(Inventory.ItemTransformation.TryTransformResult), il, out watchItemTransformationResultVar),
                               x => x.MatchCallOrCallvirt<Inventory.ItemTransformation>(nameof(Inventory.ItemTransformation.TryTransform))))
            {
                Log.PatchError(il, "Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloca, watchItemTransformationVar);
            c.Emit(OpCodes.Ldloca, watchItemTransformationResultVar);
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

        private delegate bool ConsumeQualityWatchesDelegate(bool result, HealthComponent healthComponent, in Inventory.ItemTransformation itemTransformation, ref Inventory.ItemTransformation.TryTransformResult consumeTransformResult);
    }
}
