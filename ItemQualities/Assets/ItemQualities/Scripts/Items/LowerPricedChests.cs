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

            IL.RoR2.InteractionDriver.MyFixedUpdate += ItemHooks.CombineGroupedItemCountsPatch;
            IL.RoR2.PurchaseInteraction.OnInteractionBegin += PurchaseInteraction_OnInteractionBegin;

            IL.RoR2.ChestBehavior.BaseItemDrop += ChestBehavior_BaseItemDrop;
            IL.RoR2.RouletteChestController.EjectPickupServer += RouletteChestController_EjectPickupServer;
        }

        static void CharacterMaster_TrySaleStar(On.RoR2.CharacterMaster.orig_TrySaleStar orig, CharacterMaster self)
        {
            orig(self);

            for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
            {
                ItemIndex qualitySaleStarItemIndex = ItemQualitiesContent.ItemQualityGroups.LowerPricedChests.GetItemIndex(qualityTier);
                ItemIndex qualitySaleStarConsumedItemIndex = ItemQualitiesContent.ItemQualityGroups.LowerPricedChestsConsumed.GetItemIndex(qualityTier);

                if (qualitySaleStarItemIndex != ItemIndex.None && qualitySaleStarConsumedItemIndex != ItemIndex.None)
                {
                    int saleStarCount = self.inventory.GetItemCount(qualitySaleStarConsumedItemIndex);
                    if (saleStarCount > 0)
                    {
                        self.inventory.RemoveItem(qualitySaleStarConsumedItemIndex, saleStarCount);
                        self.inventory.GiveItem(qualitySaleStarItemIndex, saleStarCount);

                        CharacterMasterNotificationQueue.SendTransformNotification(self, qualitySaleStarConsumedItemIndex, qualitySaleStarItemIndex, CharacterMasterNotificationQueue.TransformationType.SaleStarRegen);
                    }
                }
            }
        }

        class QualitySaleStarState
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
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.RemoveItem)),
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GiveItem)),
                               x => x.MatchCallOrCallvirt<CharacterMasterNotificationQueue>(nameof(CharacterMasterNotificationQueue.SendTransformNotification)),
                               x => x.MatchCallOrCallvirt<CostTypeDef>(nameof(CostTypeDef.PayCost))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            VariableDefinition saleStarStateVar = il.AddVariable<QualitySaleStarState>();
            c.Emit(OpCodes.Newobj, typeof(QualitySaleStarState).GetConstructor(Array.Empty<Type>()));
            c.Emit(OpCodes.Stloc, saleStarStateVar);

            c.Goto(foundCursors[1].Next, MoveType.Before); // call Inventory.RemoveItem

            VariableDefinition nonQualitySaleStarsCountVar = il.AddVariable<int>();

            c.Emit(OpCodes.Ldarg, interactorParameter);
            c.EmitDelegate<Func<int, Interactor, int>>(getNonQualitySaleStarsCount);
            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Stloc, nonQualitySaleStarsCountVar);
            c.EmitSkipMethodCall(OpCodes.Brfalse);

            static int getNonQualitySaleStarsCount(int totalSaleStarsCount, Interactor interactor)
            {
                CharacterBody interactorBody = interactor ? interactor.GetComponent<CharacterBody>() : null;
                Inventory interactorInventory = interactorBody ? interactorBody.inventory : null;

                if (interactorInventory)
                {
                    totalSaleStarsCount = ItemQualitiesContent.ItemQualityGroups.LowerPricedChests.GetItemCounts(interactorInventory).BaseItemCount;
                }

                return totalSaleStarsCount;
            }

            ILLabel afterVanillaSaleStarLabel = c.DefineLabel();

            c.Emit(OpCodes.Ldloc, nonQualitySaleStarsCountVar);
            c.Emit(OpCodes.Brfalse, afterVanillaSaleStarLabel);

            c.Goto(foundCursors[2].Next, MoveType.Before); // call Inventory.GiveItem

            c.Emit(OpCodes.Pop);
            c.Emit(OpCodes.Ldloc, nonQualitySaleStarsCountVar);

            c.Goto(foundCursors[3].Next, MoveType.After); // call CharacterMasterNotificationQueue.SendTransformNotification
            c.MarkLabel(afterVanillaSaleStarLabel);

            c.Emit(OpCodes.Ldarg, interactorParameter);
            c.Emit(OpCodes.Ldloc, nonQualitySaleStarsCountVar);
            c.Emit(OpCodes.Ldloc, saleStarStateVar);
            c.EmitDelegate<Action<Interactor, int, QualitySaleStarState>>(qualitySaleStarsProc);

            static void qualitySaleStarsProc(Interactor interactor, int nonQualitySaleStarsCount, QualitySaleStarState saleStarState)
            {
                CharacterBody interactorBody = interactor ? interactor.GetComponent<CharacterBody>() : null;
                CharacterMaster interactorMaster = interactorBody ? interactorBody.master : null;
                Inventory interactorInventory = interactorBody ? interactorBody.inventory : null;

                ItemQualityCounts lowerPricedChests = ItemQualitiesContent.ItemQualityGroups.LowerPricedChests.GetItemCounts(interactorInventory);
                lowerPricedChests.BaseItemCount = nonQualitySaleStarsCount;

                if (lowerPricedChests.TotalQualityCount > 0 && interactorInventory)
                {
                    for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        int qualitySaleStarCount = lowerPricedChests[qualityTier];
                        if (qualitySaleStarCount > 0)
                        {
                            ItemIndex qualitySaleStarIndex = ItemQualitiesContent.ItemQualityGroups.LowerPricedChests.GetItemIndex(qualityTier);
                            ItemIndex qualityConsumedSaleStarIndex = ItemQualitiesContent.ItemQualityGroups.LowerPricedChestsConsumed.GetItemIndex(qualityTier);

                            interactorInventory.RemoveItem(qualitySaleStarIndex, qualitySaleStarCount);
                            interactorInventory.GiveItem(qualityConsumedSaleStarIndex, qualitySaleStarCount);

                            if (interactorMaster)
                            {
                                CharacterMasterNotificationQueue.SendTransformNotification(interactorMaster, qualitySaleStarIndex, qualityConsumedSaleStarIndex, CharacterMasterNotificationQueue.TransformationType.SaleStarRegen);
                            }
                        }
                    }
                }

                if (saleStarState != null)
                {
                    saleStarState.SaleStarsSpent = lowerPricedChests;
                }
            }

            c.Goto(foundCursors[4].Next, MoveType.After); // call CostTypeDef.PayCost

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
                CostTypeDef.PayCostResults purchaseResults = useFirstInteractionCost ? purchaseContext.FirstInteractionResults : purchaseContext.Results;
                if (purchaseResults != null)
                {
                    ItemQualityCounts usedSaleStars = purchaseResults.GetUsedSaleStars();
                    if (usedSaleStars.TotalQualityCount > 0)
                    {
                        int bonusDropCount = dropCount - 1;
                        dropQualityTiers = new QualityTier[bonusDropCount];

                        int nextDropIndex = 0;
                        for (QualityTier qualityTier = QualityTier.Count - 1; qualityTier > QualityTier.None; qualityTier--)
                        {
                            int dropsOfQuality = Mathf.CeilToInt((usedSaleStars[qualityTier] / (float)usedSaleStars.TotalCount) * bonusDropCount);
                            int dropsOfQualityToAdd = Mathf.Min(dropsOfQuality, dropQualityTiers.Length - nextDropIndex);
                            if (dropsOfQualityToAdd > 0)
                            {
                                Array.Fill(dropQualityTiers, qualityTier, nextDropIndex, dropsOfQualityToAdd);
                                nextDropIndex += dropsOfQualityToAdd;

                                if (nextDropIndex >= dropQualityTiers.Length)
                                    break;
                            }
                        }

                        if (nextDropIndex < dropQualityTiers.Length)
                        {
                            Array.Fill(dropQualityTiers, QualityTier.None, nextDropIndex, dropQualityTiers.Length - nextDropIndex);
                        }

                        Util.ShuffleArray(dropQualityTiers, rng);

                        Log.Debug($"{Util.GetGameObjectHierarchyName(purchasedObject)} determined drop qualities from sale star ratios ({usedSaleStars}): [{string.Join(", ", dropQualityTiers)}]");
                    }
                }
            }

            return dropQualityTiers;
        }

        delegate PickupIndex TryUpgradePickupQualityFromSaleStarsDelegate(PickupIndex pickupIndex, QualityTier[] saleStarDropQualityTiers, ref int pickupSequenceIndex);
        static PickupIndex tryUpgradePickupQualityFromSaleStars(PickupIndex pickupIndex, QualityTier[] saleStarDropQualityTiers, ref int pickupSequenceIndex)
        {
            QualityTier pickupQuality = QualityCatalog.GetQualityTier(pickupIndex);

            if (saleStarDropQualityTiers != null && pickupSequenceIndex > 0)
            {
                QualityTier upgradeQualityTier = ArrayUtils.GetSafe(saleStarDropQualityTiers, pickupSequenceIndex - 1, QualityTier.None);

                if (upgradeQualityTier > pickupQuality)
                {
                    pickupIndex = QualityCatalog.GetPickupIndexOfQuality(pickupIndex, upgradeQualityTier);
                    pickupQuality = upgradeQualityTier;
                }
            }

            pickupSequenceIndex++;

            return pickupIndex;
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
                              x => x.MatchCallOrCallvirt<GenericPickupController.CreatePickupInfo>("set_" + nameof(GenericPickupController.CreatePickupInfo.pickupIndex))))
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
            if (!il.Method.TryFindParameter<PickupIndex>(out ParameterDefinition pickupIndexParameter))
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
                              x => x.MatchLdarg(pickupIndexParameter.Index)))
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
