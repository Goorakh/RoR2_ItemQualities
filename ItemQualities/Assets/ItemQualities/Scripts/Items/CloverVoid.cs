using ItemQualities.Utilities.Extensions;
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
