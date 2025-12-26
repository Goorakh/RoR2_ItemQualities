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

            VariableDefinition saleStarStateVar = il.AddVariable<QualitySaleStarState>();
            c.Emit(OpCodes.Newobj, typeof(QualitySaleStarState).GetConstructor(Array.Empty<Type>()));
            c.Emit(OpCodes.Stloc, saleStarStateVar);

            int saleStarItemTransformationVarIndex = -1;
            int saleStarItemTransformationResultVarIndex = -1;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdfld<PurchaseInteraction>(nameof(PurchaseInteraction.saleStarCompatible))) ||
                !c.TryGotoNext(MoveType.After,
                               x => x.MatchLdloca(typeof(Inventory.ItemTransformation), il, out saleStarItemTransformationVarIndex),
                               x => x.MatchLdloc(typeof(CharacterBody), il, out _),
                               x => x.MatchCallOrCallvirt<CharacterBody>("get_" + nameof(CharacterBody.inventory)),
                               x => x.MatchLdloca(typeof(Inventory.ItemTransformation.TryTransformResult), il, out saleStarItemTransformationResultVarIndex),
                               x => x.MatchCallOrCallvirt<Inventory.ItemTransformation>(nameof(Inventory.ItemTransformation.TryTransform))))
            {
                Log.Error("Failed to find sale star proc patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg, interactorParameter);
            c.Emit(OpCodes.Ldloca, saleStarItemTransformationVarIndex);
            c.Emit(OpCodes.Ldloca, saleStarItemTransformationResultVarIndex);
            c.Emit(OpCodes.Ldloc, saleStarStateVar);
            c.EmitDelegate<ConsumeQualitySaleStarsDelegate>(consumeQualitySaleStars);

            static bool consumeQualitySaleStars(bool result, Interactor activator, in Inventory.ItemTransformation itemTransformation, ref Inventory.ItemTransformation.TryTransformResult consumeTransformResult, QualitySaleStarState saleStarState)
            {
                CharacterBody body = activator ? activator.GetComponent<CharacterBody>() : null;
                Inventory inventory = body ? body.inventory : null;

                if (result)
                {
                    if (saleStarState != null)
                    {
                        saleStarState.SaleStarsSpent.BaseItemCount = consumeTransformResult.takenItem.stackValues.permanentStacks;
                    }
                }

                if (inventory)
                {
                    for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        Inventory.ItemTransformation qualityItemTransformation = itemTransformation;
                        qualityItemTransformation.originalItemIndex = ItemQualitiesContent.ItemQualityGroups.LowerPricedChests.GetItemIndex(qualityTier);
                        qualityItemTransformation.newItemIndex = ItemQualitiesContent.ItemQualityGroups.LowerPricedChestsConsumed.GetItemIndex(qualityTier);

                        if (qualityItemTransformation.TryTransform(inventory, out Inventory.ItemTransformation.TryTransformResult qualityConsumeTransformResult))
                        {
                            result = true;

                            static void addStackValues(ref Inventory.ItemStackValues a, in Inventory.ItemStackValues b)
                            {
                                a.permanentStacks += b.permanentStacks;
                                a.temporaryStacksValue += b.temporaryStacksValue;
                                a.totalStacks += b.totalStacks;
                            }

                            addStackValues(ref consumeTransformResult.takenItem.stackValues, qualityConsumeTransformResult.takenItem.stackValues);
                            addStackValues(ref consumeTransformResult.givenItem.stackValues, qualityConsumeTransformResult.givenItem.stackValues);

                            if (saleStarState != null)
                            {
                                saleStarState.SaleStarsSpent[qualityTier] += qualityConsumeTransformResult.takenItem.stackValues.totalStacks;
                            }
                        }
                    }
                }

                return result;
            }

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchCallOrCallvirt<CostTypeDef>(nameof(CostTypeDef.PayCost))))
            {
                Log.Error("Failed to find PayCost patch location");
                return;
            }

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

        delegate bool ConsumeQualitySaleStarsDelegate(bool result, Interactor activator, in Inventory.ItemTransformation itemTransformation, ref Inventory.ItemTransformation.TryTransformResult consumeTransformResult, QualitySaleStarState saleStarState);

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
