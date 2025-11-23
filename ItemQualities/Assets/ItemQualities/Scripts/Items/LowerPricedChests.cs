using HG;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class LowerPricedChests
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.CharacterMaster.TrySaleStar += CharacterMaster_TrySaleStar;

            IL.RoR2.PurchaseInteraction.OnInteractionBegin += PurchaseInteraction_OnInteractionBegin;

            IL.RoR2.ChestBehavior.BaseItemDrop += ChestBehavior_BaseItemDrop;
            IL.RoR2.RouletteChestController.EjectPickupServer += RouletteChestController_EjectPickupServer;
        }

        static void CharacterMaster_TrySaleStar(On.RoR2.CharacterMaster.orig_TrySaleStar orig, CharacterMaster self)
        {
            orig(self);

            if (self && self.inventory)
            {
                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    ItemIndex qualitySaleStarItemIndex = ItemQualitiesContent.ItemQualityGroups.LowerPricedChests.GetItemIndex(qualityTier);
                    ItemIndex qualitySaleStarConsumedItemIndex = ItemQualitiesContent.ItemQualityGroups.LowerPricedChestsConsumed.GetItemIndex(qualityTier);

                    if (qualitySaleStarItemIndex != ItemIndex.None && qualitySaleStarConsumedItemIndex != ItemIndex.None)
                    {
                        Inventory.ItemTransformation qualitySaleStarTransformation = new Inventory.ItemTransformation
                        {
                            originalItemIndex = qualitySaleStarConsumedItemIndex,
                            newItemIndex = qualitySaleStarItemIndex,
                            maxToTransform = int.MaxValue,
                            transformationType = (ItemTransformationTypeIndex)CharacterMasterNotificationQueue.TransformationType.SaleStarRegen
                        };

                        qualitySaleStarTransformation.TryTransform(self.inventory, out _);
                    }
                }
            }
        }

        sealed class QualitySaleStarState
        {
            public ItemQualityCounts SaleStarsSpent;
        }

        static void PurchaseInteraction_OnInteractionBegin(ILContext il)
        {
            if (!il.Method.TryFindParameter<Interactor>(out ParameterDefinition interactorParameter))
            {
                Log.Error("Failed to find Interactor parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(DLC2Content.Items), nameof(DLC2Content.Items.LowerPricedChests)),
                               x => x.MatchCallOrCallvirt<Inventory.ItemTransformation>(nameof(Inventory.ItemTransformation.TryTransform)),
                               x => x.MatchCallOrCallvirt<CostTypeDef>(nameof(CostTypeDef.PayCost))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            VariableDefinition saleStarStateVar = il.AddVariable<QualitySaleStarState>();
            c.Emit(OpCodes.Newobj, typeof(QualitySaleStarState).GetConstructor(Array.Empty<Type>()));
            c.Emit(OpCodes.Stloc, saleStarStateVar);

            c.Goto(foundCursors[1].Next, MoveType.After); // call Inventory.ItemTransformation.TryTransform

            if (!ItemHooks.EmitCombinedQualityItemTransformationPatch(c, out VariableDefinition transformedQualityStacksVar))
                return;

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldloc, transformedQualityStacksVar);
            c.Emit(OpCodes.Ldloc, saleStarStateVar);
            c.EmitDelegate<Action<bool, QualityItemTransformResult, QualitySaleStarState>>(onSaleStarProc);

            static void onSaleStarProc(bool success, QualityItemTransformResult transformResult, QualitySaleStarState saleStarState)
            {
                if (success)
                {
                    saleStarState.SaleStarsSpent = transformResult.TakenItems.StackValues.TotalStacks;
                }
            }

            c.Goto(foundCursors[2].Next, MoveType.Before); // call CostTypeDef.PayCost

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldloc, saleStarStateVar);
            c.EmitDelegate<Action<CostTypeDef.PayCostResults, QualitySaleStarState>>(onPayCost);

            static void onPayCost(CostTypeDef.PayCostResults results, QualitySaleStarState saleStarState)
            {
                if (results != null && saleStarState != null)
                {
                    results.SetUsedSaleStars(saleStarState.SaleStarsSpent);
                }
            }
        }

        static QualityTier[] generateQualityDropTiersFromSaleStars(GameObject purchasedObject, int dropCount, bool useFirstInteractionCost, Xoroshiro128Plus rng)
        {
            QualityTier[] dropQualityTiers = Array.Empty<QualityTier>();

            if (dropCount > 0 && purchasedObject && purchasedObject.TryGetComponent(out ObjectPurchaseContext purchaseContext))
            {
                ObjectPurchaseContext.PurchaseResults purchaseResults = useFirstInteractionCost ? purchaseContext.FirstInteractionResults : purchaseContext.Results;
                if (purchaseResults != null)
                {
                    ItemQualityCounts usedSaleStars = purchaseResults.UsedSaleStarCounts;
                    if (usedSaleStars.TotalQualityCount > 0)
                    {
                        int bonusDropCount = dropCount - 1;
                        Span<QualityTier> dropQualityTiersSpan = stackalloc QualityTier[bonusDropCount];

                        int nextDropIndex = 0;
                        for (QualityTier qualityTier = QualityTier.Count - 1; qualityTier > QualityTier.None; qualityTier--)
                        {
                            int dropsOfQuality = Mathf.CeilToInt((usedSaleStars[qualityTier] / (float)usedSaleStars.TotalCount) * bonusDropCount);
                            int dropsOfQualityToAdd = Mathf.Min(dropsOfQuality, dropQualityTiersSpan.Length - nextDropIndex);
                            if (dropsOfQualityToAdd > 0)
                            {
                                dropQualityTiersSpan.Slice(nextDropIndex, dropsOfQualityToAdd).Fill(qualityTier);
                                nextDropIndex += dropsOfQualityToAdd;

                                if (nextDropIndex >= dropQualityTiersSpan.Length)
                                    break;
                            }
                        }

                        if (nextDropIndex < dropQualityTiersSpan.Length)
                        {
                            dropQualityTiersSpan.Slice(nextDropIndex).Fill(QualityTier.None);
                        }

                        Util.ShuffleSpan(dropQualityTiersSpan, rng);

                        dropQualityTiers = dropQualityTiersSpan.ToArray();

                        Log.Debug($"{Util.GetGameObjectHierarchyName(purchasedObject)} determined drop qualities from sale star ratios ({usedSaleStars}): [{string.Join(", ", dropQualityTiers)}]");
                    }
                }
            }

            return dropQualityTiers;
        }

        delegate UniquePickup TryUpgradePickupQualityFromSaleStarsDelegate(UniquePickup pickup, QualityTier[] saleStarDropQualityTiers, ref int pickupSequenceIndex);
        static UniquePickup tryUpgradePickupQualityFromSaleStars(UniquePickup pickup, QualityTier[] saleStarDropQualityTiers, ref int pickupSequenceIndex)
        {
            QualityTier pickupQuality = QualityCatalog.GetQualityTier(pickup.pickupIndex);

            if (saleStarDropQualityTiers != null && pickupSequenceIndex > 0)
            {
                QualityTier upgradeQualityTier = ArrayUtils.GetSafe(saleStarDropQualityTiers, pickupSequenceIndex - 1, QualityTier.None);

                if (upgradeQualityTier > pickupQuality)
                {
                    pickup = pickup.WithQualityTier(upgradeQualityTier);
                    pickupQuality = upgradeQualityTier;
                }
            }

            pickupSequenceIndex++;

            return pickup;
        }

        static void ChestBehavior_BaseItemDrop(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition dropQualityIndexVar = il.AddVariable<int>();
            c.Emit(OpCodes.Ldc_I4_0);
            c.Emit(OpCodes.Stloc, dropQualityIndexVar);

            VariableDefinition dropQualityTiersVar = il.AddVariable<QualityTier[]>();
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<ChestBehavior, QualityTier[]>>(getUpgradeQualityTiers);
            c.Emit(OpCodes.Stloc, dropQualityTiersVar);

            static QualityTier[] getUpgradeQualityTiers(ChestBehavior chestBehavior)
            {
                if (!chestBehavior)
                    return Array.Empty<QualityTier>();

                Xoroshiro128Plus saleStarRng = new Xoroshiro128Plus(chestBehavior.rng.nextUlong);
                return generateQualityDropTiersFromSaleStars(chestBehavior.gameObject, chestBehavior.dropCount, false, saleStarRng);
            }

            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchCallOrCallvirt<PickupDropletController>(nameof(PickupDropletController.CreatePickupDroplet))) &&
                c.TryGotoPrev(MoveType.Before,
                              x => x.MatchCallOrCallvirt<GenericPickupController.CreatePickupInfo>("set_" + nameof(GenericPickupController.CreatePickupInfo.pickup))))
            {
                c.Emit(OpCodes.Ldloc, dropQualityTiersVar);
                c.Emit(OpCodes.Ldloca, dropQualityIndexVar);
                c.EmitDelegate<TryUpgradePickupQualityFromSaleStarsDelegate>(tryUpgradePickupQualityFromSaleStars);
            }
            else
            {
                Log.Error("Failed to find patch location");
            }
        }

        static void RouletteChestController_EjectPickupServer(ILContext il)
        {
            if (!il.Method.TryFindParameter<UniquePickup>(out ParameterDefinition pickupParameter))
            {
                Log.Error("Failed to find PickupIndex parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            VariableDefinition dropQualityIndexVar = il.AddVariable<int>();
            c.Emit(OpCodes.Ldc_I4_0);
            c.Emit(OpCodes.Stloc, dropQualityIndexVar);

            VariableDefinition dropQualityTiersVar = il.AddVariable<QualityTier[]>();
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<RouletteChestController, QualityTier[]>>(getUpgradeQualityTiers);
            c.Emit(OpCodes.Stloc, dropQualityTiersVar);

            static QualityTier[] getUpgradeQualityTiers(RouletteChestController rouletteChestController)
            {
                if (!rouletteChestController)
                    return Array.Empty<QualityTier>();

                Xoroshiro128Plus saleStarRng = new Xoroshiro128Plus(rouletteChestController.rng.nextUlong);
                return generateQualityDropTiersFromSaleStars(rouletteChestController.gameObject, rouletteChestController.dropCount, true, saleStarRng);
            }

            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchCallOrCallvirt<PickupDropletController>(nameof(PickupDropletController.CreatePickupDroplet))) &&
                c.TryGotoPrev(MoveType.After,
                              x => x.MatchLdarg(pickupParameter.Index)))
            {
                c.Emit(OpCodes.Ldloc, dropQualityTiersVar);
                c.Emit(OpCodes.Ldloca, dropQualityIndexVar);
                c.EmitDelegate<TryUpgradePickupQualityFromSaleStarsDelegate>(tryUpgradePickupQualityFromSaleStars);
            }
            else
            {
                Log.Error("Failed to find patch location");
            }
        }
    }
}
