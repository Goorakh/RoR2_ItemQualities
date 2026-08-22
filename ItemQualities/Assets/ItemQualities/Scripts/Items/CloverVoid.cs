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
    internal static class CloverVoid
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.CharacterMaster.TryCloverVoidUpgrades += CharacterMaster_TryCloverVoidUpgrades;
        }

        private static void CharacterMaster_TryCloverVoidUpgrades(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int localsVar = -1;
            FieldReference startingItemDefLocalsField = null;
            if (c.Clone().TryGotoNext(MoveType.Before,
                                      x => x.MatchLdloc(out localsVar),
                                      x => x.MatchLdfld(out startingItemDefLocalsField) && startingItemDefLocalsField?.Name == "startingItemDef"))
            {
                VariableDefinition tier2DropListVar = null;
                if (c.TryGotoNext(MoveType.After,
                                  x => x.MatchLdfld<Run>(nameof(Run.availableTier2DropList)),
                                  x => x.MatchNewobj<List<PickupIndex>>(),
                                  x => x.MatchStloc(typeof(List<PickupIndex>), il, out tier2DropListVar)))
                {
                    patchDropList(tier2DropListVar, "tier2");
                }
                else
                {
                    Log.Error("Failed to find tier2 droplist variable");
                }

                c.Index = 0;

                VariableDefinition tier3DropListVar = null;
                if (c.TryGotoNext(MoveType.After,
                                  x => x.MatchLdfld<Run>(nameof(Run.availableTier3DropList)),
                                  x => x.MatchNewobj<List<PickupIndex>>(),
                                  x => x.MatchStloc(typeof(List<PickupIndex>), il, out tier3DropListVar)))
                {
                    patchDropList(tier3DropListVar, "tier3");
                }
                else
                {
                    Log.Error("Failed to find tier3 droplist variable");
                }

                void patchDropList(VariableDefinition tierDropListVar, string name)
                {
                    VariableDefinition tierDropListsByQualityVar = il.AddVariable<List<PickupIndex>[]>();

                    c.Emit(OpCodes.Ldc_I4, (int)QualityTier.Count);
                    c.Emit(OpCodes.Newarr, typeof(List<PickupIndex>));

                    for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        c.Emit(OpCodes.Dup);
                        c.Emit(OpCodes.Ldc_I4, (int)qualityTier);
                        c.Emit(OpCodes.Ldloc, tierDropListVar);
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
                                      x => x.MatchLdloc(tierDropListVar),
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

            VariableDefinition upgradableItemListVar = null;
            if (!c.TryFindNext(out _,
                               x => x.MatchLdfld<Inventory>(nameof(Inventory.itemAcquisitionOrder)),
                               x => x.MatchStloc(typeof(List<ItemIndex>), il, out upgradableItemListVar)))
            {
                Log.Error("Failed to find upgradableItems list variable");
            }

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchCallOrCallvirt<Inventory.ItemTransformation>(nameof(Inventory.ItemTransformation.TryTransform))))
            {
                Log.Error("Failed to find ItemTransformation call location");
                return;
            }

            VariableDefinition itemTransformationVar = null;
            if (!c.TryFindPrev(out _,
                               x => x.MatchLdloca(typeof(Inventory.ItemTransformation), il, out itemTransformationVar),
                               x => x.MatchInitobj<Inventory.ItemTransformation>()))
            {
                Log.Error("Failed to find ItemTransformation variable");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, itemTransformationVar);

            if (upgradableItemListVar != null)
            {
                c.Emit(OpCodes.Ldloc, upgradableItemListVar);
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

                float qualityUpgradeChance = (0.10f * cloverVoid.UncommonCount) +
                                             (0.25f * cloverVoid.RareCount) +
                                             (0.35f * cloverVoid.EpicCount) +
                                             (0.50f * cloverVoid.LegendaryCount);

                QualityTier maxUpgradableQualityTier = cloverVoid.HighestQuality - 1;

                using var _ = ListPool<QualityTier>.RentCollection(out List<QualityTier> upgradableItemQualityTiers);
                ListUtils.EnsureCapacity(upgradableItemQualityTiers, startingItemCount);

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
                        QualityTier qualityTierToUpgrade = ListUtils.Take(upgradableItemQualityTiers, 0);
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
