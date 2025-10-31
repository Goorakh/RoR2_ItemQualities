using HG;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
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

            if (c.TryFindNext(out ILCursor[] foundCursors,
                              x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.CloverVoid)),
                              x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount))))
            {
                c.Goto(foundCursors[1].Next, MoveType.Before); // call Inventory.GetItemCount
                ItemHooks.EmitSingleCombineGroupedItemCounts(c);
                c.Goto(0, MoveType.Before);
            }
            else
            {
                Log.Error("Failed to find CloverVoid ItemCount patch location");
            }

            int upgradableItemListVarIndex = -1;
            if (!c.TryFindNext(out _,
                               x => x.MatchLdfld<Inventory>(nameof(Inventory.itemAcquisitionOrder)),
                               x => x.MatchStloc(typeof(List<ItemIndex>), il, out upgradableItemListVarIndex)))
            {
                Log.Error("Failed to find upgradableItems list variable");
            }

            VariableDefinition startingItemDefVar = il.AddVariable<ItemDef>();

            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchStfld(out FieldReference f) && f?.Name == "startingItemDef" && f?.FieldType?.Is(typeof(ItemDef)) == true))
            {
                c.Emit(OpCodes.Dup);
                c.Emit(OpCodes.Stloc, startingItemDefVar);

                c.Goto(0, MoveType.Before);
            }
            else
            {
                Log.Error("Failed to find startingItemDef patch location");
            }

            int upgradedItemDefVarIndex = -1;
            if (!c.TryFindNext(out foundCursors,
                               x => x.MatchLdftn(out MethodReference m) && m?.Name?.StartsWith("<TryCloverVoidUpgrades>g__CompareTags|") == true,
                               x => x.MatchStloc(typeof(ItemDef), il, out upgradedItemDefVarIndex)))
            {
                Log.Error("Failed to find upgraded item variable");
                return;
            }

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<CharacterMasterNotificationQueue>(nameof(CharacterMasterNotificationQueue.SendTransformNotification))))
            {
                Log.Error("Failed to find item transformation end location");
                return;
            }

            ILLabel afterItemTransformLabel = c.MarkLabel();

            if (!c.TryGotoPrev(MoveType.Before,
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.RemoveItem))))
            {
                Log.Error("Failed to find item transformation start location");
                return;
            }

            VariableDefinition startingItemCountVar = il.AddVariable<int>();
            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Stloc, startingItemCountVar);

            c.Goto(c.Next, MoveType.After);

            VariableDefinition upgradeItemQualitiesVar = il.AddVariable<ItemQualityCounts>();

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, startingItemDefVar);
            c.Emit(OpCodes.Ldloc, startingItemCountVar);
            c.EmitDelegate<Func<CharacterMaster, ItemDef, int, ItemQualityCounts>>(rollItemQualities);
            c.Emit(OpCodes.Stloc, upgradeItemQualitiesVar);

            static ItemQualityCounts rollItemQualities(CharacterMaster master, ItemDef startingItemDef, int startingItemCount)
            {
                ItemIndex startingItemIndex = startingItemDef ? startingItemDef.itemIndex : ItemIndex.None;
                QualityTier startingQualityTier = QualityCatalog.GetQualityTier(startingItemIndex);

                ItemQualityCounts upgradeItemQualities = new ItemQualityCounts();
                upgradeItemQualities[startingQualityTier] = startingItemCount;

                Inventory inventory = master ? master.inventory : null;

                ItemQualityCounts cloverVoid = ItemQualitiesContent.ItemQualityGroups.CloverVoid.GetItemCounts(inventory);
                if (cloverVoid.TotalQualityCount > 0)
                {
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
                }

                return upgradeItemQualities;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, startingItemDefVar);
            c.Emit(OpCodes.Ldloc, upgradedItemDefVarIndex);
            c.Emit(OpCodes.Ldloc, upgradeItemQualitiesVar);

            if (upgradableItemListVarIndex != -1)
            {
                c.Emit(OpCodes.Ldloc, upgradableItemListVarIndex);
            }
            else
            {
                c.Emit(OpCodes.Ldnull);
            }

            c.EmitDelegate<Action<CharacterMaster, ItemDef, ItemDef, ItemQualityCounts, List<ItemIndex>>>(tryQualityItemTransformations);

            static void tryQualityItemTransformations(CharacterMaster master, ItemDef startingItemDef, ItemDef upgradedItemDef, ItemQualityCounts upgradeItemQualities, List<ItemIndex> upgradableItems)
            {
                ItemIndex startingItemIndex = startingItemDef ? startingItemDef.itemIndex : ItemIndex.None;
                ItemIndex upgradedItemIndex = upgradedItemDef ? upgradedItemDef.itemIndex : ItemIndex.None;

                for (QualityTier qualityTier = QualityTier.Count - 1; qualityTier >= 0; qualityTier--)
                {
                    ItemIndex qualityUpgradedItemIndex = QualityCatalog.GetItemIndexOfQuality(upgradedItemIndex, qualityTier);

                    if (qualityUpgradedItemIndex != ItemIndex.None)
                    {
                        int qualityItemCount = upgradeItemQualities[qualityTier];
                        if (qualityItemCount > 0)
                        {
                            if (upgradableItems != null && master.inventory.GetItemCount(qualityUpgradedItemIndex) == 0)
                            {
                                upgradableItems.Add(qualityUpgradedItemIndex);
                            }

                            master.inventory.GiveItem(qualityUpgradedItemIndex, qualityItemCount);

                            if (startingItemIndex != ItemIndex.None)
                            {
                                CharacterMasterNotificationQueue.SendTransformNotification(master, startingItemIndex, qualityUpgradedItemIndex, CharacterMasterNotificationQueue.TransformationType.CloverVoid);
                            }
                        }
                    }
                }
            }

            c.Emit(OpCodes.Ldloc, upgradeItemQualitiesVar);
            c.EmitDelegate<Func<ItemQualityCounts, int>>(getBaseQualityItemTransformations);
            c.Emit(OpCodes.Ldc_I4_0);
            c.Emit(OpCodes.Ble, afterItemTransformLabel);

            static int getBaseQualityItemTransformations(ItemQualityCounts upgradeItemQualities)
            {
                return upgradeItemQualities.BaseItemCount;
            }

            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GiveItem))))
            {
                c.Emit(OpCodes.Pop);

                c.Emit(OpCodes.Ldloc, upgradeItemQualitiesVar);
                c.EmitDelegate<Func<ItemQualityCounts, int>>(getBaseQualityItemTransformations);
            }
            else
            {
                Log.Error("Failed to find base quality GiveItem patch location");
            }
        }
    }
}
