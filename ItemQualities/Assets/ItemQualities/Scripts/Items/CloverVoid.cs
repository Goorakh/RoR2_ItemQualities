using HG;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Collections.Generic;

namespace ItemQualities.Items
{
    static class CloverVoid
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.CharacterMaster.TryCloverVoidUpgrades += CharacterMaster_TryCloverVoidUpgrades;
        }

        static void CharacterMaster_TryCloverVoidUpgrades(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int localsVar = -1;
            FieldReference startingItemDefLocalsField = null;
            if (c.Clone().TryGotoNext(MoveType.Before,
                                      x => x.MatchLdloc(out localsVar),
                                      x => x.MatchLdfld(out startingItemDefLocalsField) && startingItemDefLocalsField?.Name == "startingItemDef"))
            {
                int tier2DropListVarIndex = -1;
                if (c.TryGotoNext(MoveType.After,
                                  x => x.MatchLdfld<Run>(nameof(Run.availableTier2DropList)),
                                  x => x.MatchNewobj<List<PickupIndex>>(),
                                  x => x.MatchStloc(typeof(List<PickupIndex>), il, out tier2DropListVarIndex)))
                {
                    patchDropList(tier2DropListVarIndex, "tier2");
                }
                else
                {
                    Log.Error("Failed to find tier2 droplist variable");
                }

                c.Index = 0;

                int tier3DropListVarIndex = -1;
                if (c.TryGotoNext(MoveType.After,
                                  x => x.MatchLdfld<Run>(nameof(Run.availableTier3DropList)),
                                  x => x.MatchNewobj<List<PickupIndex>>(),
                                  x => x.MatchStloc(typeof(List<PickupIndex>), il, out tier3DropListVarIndex)))
                {
                    patchDropList(tier3DropListVarIndex, "tier3");
                }
                else
                {
                    Log.Error("Failed to find tier3 droplist variable");
                }

                void patchDropList(int tierDropListVarIndex, string name)
                {
                    VariableDefinition tierDropListsByQualityVar = il.AddVariable<List<PickupIndex>[]>();

                    c.Emit(OpCodes.Ldc_I4, (int)QualityTier.Count);
                    c.Emit(OpCodes.Newarr, typeof(List<PickupIndex>));

                    for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        c.Emit(OpCodes.Dup);
                        c.Emit(OpCodes.Ldc_I4, (int)qualityTier);
                        c.Emit(OpCodes.Ldloc, tierDropListVarIndex);
                        c.Emit(OpCodes.Ldc_I4, (int)qualityTier);
                        c.EmitDelegate<Func<List<PickupIndex>, QualityTier, List<PickupIndex>>>(getQualityPickupsList);
                        c.Emit(OpCodes.Stelem_Ref);

                        static List<PickupIndex> getQualityPickupsList(List<PickupIndex> pickupIndices, QualityTier qualityTier)
                        {
                            List<PickupIndex> qualityPickupIndices = new List<PickupIndex>(pickupIndices.Count);

                            foreach (PickupIndex pickupIndex in pickupIndices)
                            {
                                PickupIndex qualityPickupIndex = QualityCatalog.GetPickupIndexOfQuality(pickupIndex, qualityTier);
                                if (qualityPickupIndex.isValid && qualityPickupIndex != pickupIndex)
                                {
                                    qualityPickupIndices.Add(qualityPickupIndex);
                                }
                            }

                            qualityPickupIndices.TrimExcess();
                            return qualityPickupIndices;
                        }
                    }

                    c.Emit(OpCodes.Stloc, tierDropListsByQualityVar);

                    if (c.TryGotoNext(MoveType.After,
                                      x => x.MatchLdloc(tierDropListVarIndex),
                                      x => x.MatchStloc(typeof(List<PickupIndex>), il, out _)))
                    {
                        c.Index--;

                        c.Emit(OpCodes.Ldloc, tierDropListsByQualityVar);

                        c.Emit(OpCodes.Ldloc, localsVar);
                        c.Emit(OpCodes.Ldfld, startingItemDefLocalsField);

                        c.EmitDelegate<Func<List<PickupIndex>, List<PickupIndex>[], ItemDef, List<PickupIndex>>>(getAvailablePickupList);

                        static List<PickupIndex> getAvailablePickupList(List<PickupIndex> availableDropList, List<PickupIndex>[] availableDropListsByQuality, ItemDef startingItemDef)
                        {
                            QualityTier startingQualityTier = QualityCatalog.GetQualityTier(startingItemDef ? startingItemDef.itemIndex : ItemIndex.None);

                            List<PickupIndex> availableQualityDropList = null;
                            if (availableDropListsByQuality != null)
                            {
                                availableQualityDropList = ArrayUtils.GetSafe(availableDropListsByQuality, (int)startingQualityTier);
                            }

                            return availableQualityDropList ?? availableDropList;
                        }
                    }
                    else
                    {
                        Log.Error($"Failed to find {name} available transformations set location");
                    }
                }
            }
            else
            {
                Log.Error("Failed to find locals variable");
            }

            c.Index = 0;

            int upgradableItemListVarIndex = -1;
            if (!c.TryFindNext(out _,
                               x => x.MatchLdfld<Inventory>(nameof(Inventory.itemAcquisitionOrder)),
                               x => x.MatchStloc(typeof(List<ItemIndex>), il, out upgradableItemListVarIndex)))
            {
                Log.Error("Failed to find upgradableItems list variable");
            }

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchCallOrCallvirt<Inventory.ItemTransformation>(nameof(Inventory.ItemTransformation.TryTransform))))
            {
                Log.Fatal("Failed to find ItemTransformation call location");
                return;
            }

            int itemTransformationLocalIndex = -1;
            if (!c.TryFindPrev(out _,
                               x => x.MatchLdloca(typeof(Inventory.ItemTransformation), il, out itemTransformationLocalIndex),
                               x => x.MatchInitobj<Inventory.ItemTransformation>()))
            {
                Log.Fatal("Failed to find ItemTransformation variable");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, itemTransformationLocalIndex);

            if (upgradableItemListVarIndex != -1)
            {
                c.Emit(OpCodes.Ldloc, upgradableItemListVarIndex);
            }
            else
            {
                c.Emit(OpCodes.Ldnull);
            }

            c.EmitDelegate<Action<CharacterMaster, Inventory.ItemTransformation, List<ItemIndex>>>(upgradeItemQualities);

            static void upgradeItemQualities(CharacterMaster master, Inventory.ItemTransformation itemTransformation, List<ItemIndex> upgradableItems)
            {
                Inventory inventory = master ? master.inventory : null;
                if (!inventory)
                    return;

                ItemQualityCounts cloverVoid = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.CloverVoid);
                if (cloverVoid.TotalQualityCount <= 0)
                    return;

                ItemIndex startingItemIndex = itemTransformation.originalItemIndex;
                int startingItemCount = inventory.CalculateEffectiveItemStacks(startingItemIndex);

                QualityTier startingQualityTier = QualityCatalog.GetQualityTier(startingItemIndex);

                ItemQualityCounts upgradeItemQualities = new ItemQualityCounts();
                upgradeItemQualities[startingQualityTier] = startingItemCount;

                float qualityUpgradeChance = Util.ConvertAmplificationPercentageIntoReductionNormalized(amplificationNormal:
                    (0.10f * cloverVoid.UncommonCount) +
                    (0.25f * cloverVoid.RareCount) +
                    (0.35f * cloverVoid.EpicCount) +
                    (0.50f * cloverVoid.LegendaryCount));

                QualityTier maxUpgradableQualityTier = cloverVoid.HighestQuality - 1;

                List<QualityTier> upgradableItemQualityTiers = new List<QualityTier>(startingItemCount);
                for (QualityTier qualityTier = QualityTier.None; qualityTier <= maxUpgradableQualityTier; qualityTier++)
                {
                    int qualityCount = upgradeItemQualities[qualityTier];
                    for (int i = 0; i < qualityCount; i++)
                    {
                        upgradableItemQualityTiers.Add(qualityTier);
                    }
                }

                int upgradeRollCount = startingItemCount;

                for (int i = 0; i < upgradeRollCount && upgradableItemQualityTiers.Count > 0; i++)
                {
                    Util.ShuffleList(upgradableItemQualityTiers, master.cloverVoidRng);

                    if (master.cloverVoidRng.nextNormalizedFloat < qualityUpgradeChance)
                    {
                        QualityTier qualityTierToUpgrade = upgradableItemQualityTiers.GetAndRemoveAt<QualityTier>(0);
                        QualityTier upgradedQualityTier = qualityTierToUpgrade + 1;

                        upgradeItemQualities[qualityTierToUpgrade]--;
                        upgradeItemQualities[upgradedQualityTier]++;

                        if (upgradedQualityTier <= maxUpgradableQualityTier)
                        {
                            upgradableItemQualityTiers.Add(upgradedQualityTier);
                        }
                    }
                }

                for (QualityTier qualityTier = QualityTier.Count - 1; qualityTier >= 0; qualityTier--)
                {
                    int itemCount = upgradeItemQualities[qualityTier];
                    if (itemCount > 0)
                    {
                        Inventory.ItemTransformation qualityItemTransformation = itemTransformation;
                        qualityItemTransformation.newItemIndex = QualityCatalog.GetItemIndexOfQuality(itemTransformation.newItemIndex, qualityTier);
                        qualityItemTransformation.maxToTransform = itemCount;

                        if (qualityItemTransformation.newItemIndex != itemTransformation.newItemIndex)
                        {
                            if (qualityItemTransformation.TryTransform(inventory, out Inventory.ItemTransformation.TryTransformResult transformResult))
                            {
                                if (upgradableItems != null && !upgradableItems.Contains(transformResult.givenItem.itemIndex))
                                {
                                    upgradableItems.Add(transformResult.givenItem.itemIndex);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
