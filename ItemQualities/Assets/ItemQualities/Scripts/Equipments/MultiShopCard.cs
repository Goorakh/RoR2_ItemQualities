using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Audio;
using RoR2.DirectionalSearch;
using RoR2.UI;
using System;
using UnityEngine;

namespace ItemQualities.Equipments
{
    static class MultiShopCard
    {
        static readonly InteractableSearch _sharedInteractableSearch = new InteractableSearch
        {
            requireCanCopy = true,
            requireSpawnCard = true,
            forbidDuplicated = true,
        };

        static readonly float _interactableSearchMinDistance = 0f;
        static readonly float _interactableSearchMaxDistance = 15f;

        static readonly float _interactableSearchMinAngle = 0f;
        static readonly float _interactableSearchMaxAngle = 12.5f;

        static readonly bool _interactableSearchFilterByLoS = false;
        static readonly bool _interactableSearchFilterByDistinctEntity = true;

        static readonly SortMode _interactableSearchSortMode = SortMode.Angle;

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.EquipmentSlot.UpdateTargets += EquipmentSlot_UpdateTargets;

            On.RoR2.EquipmentSlot.PerformEquipmentAction += EquipmentSlot_PerformEquipmentAction;

            IL.RoR2.UI.EquipmentIcon.SetDisplayData += EquipmentIcon_SetDisplayData;

            SceneDirector.onPostPopulateSceneServer += onPostPopulateSceneServer;
        }

        static void onPostPopulateSceneServer(SceneDirector sceneDirector)
        {
            if (SceneInfo.instance.countsAsStage || SceneInfo.instance.sceneDef.allowItemsToSpawnObjects)
            {
                Xoroshiro128Plus cardInteractablesRng = new Xoroshiro128Plus(sceneDirector.rng.nextUlong);

                foreach (CharacterMaster master in CharacterMaster.readOnlyInstancesList)
                {
                    if (master.TryGetComponentCached(out CharacterMasterExtraStatsTracker masterExtraStats) &&
                        masterExtraStats.CardStoredInteractableInfo.InteractableIndex != -1)
                    {
                        StoredInteractableInfo storedInteractableInfo = masterExtraStats.CardStoredInteractableInfo;

                        QualityTier cardQualityTier = QualityTier.None;

                        int equipmentSlotCount = master.inventory.GetEquipmentSlotCount();
                        for (uint slot = 0; slot < equipmentSlotCount; slot++)
                        {
                            int equipmentSetCount = master.inventory.GetEquipmentSetCount(slot);
                            for (uint set = 0; set < equipmentSetCount; set++)
                            {
                                EquipmentState equipmentState = master.inventory.GetEquipment(slot, set);
                                EquipmentQualityGroupIndex equipmentGroupIndex = QualityCatalog.FindEquipmentQualityGroupIndex(equipmentState.equipmentIndex);
                                if (equipmentGroupIndex == ItemQualitiesContent.EquipmentQualityGroups.MultiShopCard.GroupIndex)
                                {
                                    QualityTier qualityTier = QualityCatalog.GetQualityTier(equipmentState.equipmentIndex);
                                    if (qualityTier > cardQualityTier)
                                    {
                                        cardQualityTier = qualityTier;
                                    }
                                }
                            }
                        }

                        if (cardQualityTier > QualityTier.None)
                        {
                            InteractableDef interactableDef = InteractableCatalog.GetInteractableDef(storedInteractableInfo.InteractableIndex);

                            float spawnChance;
                            switch (cardQualityTier)
                            {
                                case QualityTier.Uncommon:
                                    spawnChance = 100f;
                                    break;
                                case QualityTier.Rare:
                                    spawnChance = 130f;
                                    break;
                                case QualityTier.Epic:
                                    spawnChance = 180f;
                                    break;
                                case QualityTier.Legendary:
                                    spawnChance = 220f;
                                    break;
                                default:
                                    spawnChance = 100f;
                                    Log.Warning($"Quality tier {cardQualityTier} is not implemented");
                                    break;
                            }

                            int spawnCount = RollUtil.GetOverflowRoll(spawnChance, cardInteractablesRng);

                            for (int i = 0; i < spawnCount; i++)
                            {
                                DirectorSpawnRequest directorSpawnRequest = new DirectorSpawnRequest(interactableDef.SpawnCard, new DirectorPlacementRule
                                {
                                    placementMode = DirectorPlacementRule.PlacementMode.Random,
                                }, cardInteractablesRng);

                                directorSpawnRequest.onSpawnedServer += onSpawnedServer;

                                DirectorCore.instance.TrySpawnObject(directorSpawnRequest);

                                void onSpawnedServer(SpawnCard.SpawnResult spawnResult)
                                {
                                    if (!spawnResult.success || !spawnResult.spawnedInstance)
                                        return;
                                    
                                    if (spawnResult.spawnedInstance.TryGetComponent(out PurchaseInteraction purchaseInteraction))
                                    {
                                        if (purchaseInteraction.costType == CostTypeIndex.Money && !purchaseInteraction.automaticallyScaleCostWithDifficulty)
                                        {
                                            purchaseInteraction.Networkcost = Run.instance.GetDifficultyScaledCost(purchaseInteraction.cost);
                                        }
                                    }

                                    if (spawnResult.spawnedInstance.TryGetComponent(out SummonMasterBehavior summonMasterBehavior))
                                    {
                                        summonMasterBehavior.NetworkdroneUpgradeCount = storedInteractableInfo.UpgradeValue;
                                    }

                                    if (spawnResult.spawnedInstance.TryGetComponent(out InteractableInfoProvider interactableInfo))
                                    {
                                        interactableInfo.Duplicated = true;
                                    }

                                    EffectData effectData = new EffectData
                                    {
                                        origin = spawnResult.spawnedInstance.transform.position,
                                    };

                                    effectData.SetNetworkedObjectReference(spawnResult.spawnedInstance);

                                    EffectManager.SpawnEffect(ItemQualitiesContent.Prefabs.DuplicatedInteractableEffect, effectData, true);
                                }
                            }

                            Log.Debug($"Spawned {spawnCount}x {interactableDef} for {Util.GetBestMasterName(master)}");
                        }

                        masterExtraStats.CardStoredInteractableInfo = StoredInteractableInfo.None;
                    }
                }
            }
        }

        static void EquipmentSlot_UpdateTargets(ILContext il)
        {
            if (!il.Method.TryFindParameter<EquipmentIndex>("targetingEquipmentIndex", out ParameterDefinition targetingEquipmentIndexParameter))
            {
                Log.Error("Failed to find 'targetingEquipmentIndex' parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.AfterLabel,
                              x => x.MatchLdarg(0),
                              x => x.MatchLdflda<EquipmentSlot>(nameof(EquipmentSlot.currentTarget)),
                              x => x.MatchInitobj<EquipmentSlot.UserTargetInfo>()))
            {
                ILLabel setTargetIfElseEndLabel = null;
                if (c.Clone().TryGotoPrev(x => x.MatchBr(out setTargetIfElseEndLabel) && c.IsBefore(setTargetIfElseEndLabel.Target)))
                {
                    c.Emit(OpCodes.Ldarg_0);
                    c.Emit(OpCodes.Ldarg, targetingEquipmentIndexParameter);
                    c.EmitDelegate<Func<EquipmentSlot, EquipmentIndex, bool>>(trySetInteractableTarget);
                    c.Emit(OpCodes.Brtrue, setTargetIfElseEndLabel);

                    static bool trySetInteractableTarget(EquipmentSlot equipmentSlot, EquipmentIndex targetingEquipmentIndex)
                    {
                        if (equipmentSlot.GetActiveEquipmentQualityTier() == QualityTier.None)
                            return false;

                        EquipmentQualityGroupIndex targetingEquipmentGroup = QualityCatalog.FindEquipmentQualityGroupIndex(targetingEquipmentIndex);
                        if (targetingEquipmentGroup != ItemQualitiesContent.EquipmentQualityGroups.MultiShopCard.GroupIndex)
                            return false;

                        Ray ray = equipmentSlot.GetAimRay();

                        ray = CameraRigController.ModifyAimRayIfApplicable(ray, equipmentSlot.gameObject, out float extraRaycastDistance);

                        _sharedInteractableSearch.searchOrigin = ray.origin;
                        _sharedInteractableSearch.searchDirection = ray.direction;

                        _sharedInteractableSearch.minAngleFilter = _interactableSearchMinAngle;
                        _sharedInteractableSearch.maxAngleFilter = _interactableSearchMaxAngle;

                        _sharedInteractableSearch.minDistanceFilter = _interactableSearchMinDistance;
                        _sharedInteractableSearch.maxDistanceFilter = _interactableSearchMaxDistance + extraRaycastDistance;

                        _sharedInteractableSearch.filterByLoS = _interactableSearchFilterByLoS;
                        _sharedInteractableSearch.filterByDistinctEntity = _interactableSearchFilterByDistinctEntity;

                        _sharedInteractableSearch.sortMode = _interactableSearchSortMode;

                        InteractableInfoProvider targetInteractable = _sharedInteractableSearch.SearchCandidatesForSingleTarget(InstanceTracker.GetInstancesList<InteractableInfoProvider>());

                        equipmentSlot.currentTarget = new EquipmentSlot.UserTargetInfo
                        {
                            rootObject = targetInteractable ? targetInteractable.gameObject : null,
                            transformToIndicateAt = targetInteractable ? targetInteractable.IndicatorTransform : null
                        };

                        return true;
                    }
                }
                else
                {
                    Log.Error("Failed to find target if-else end location");
                }
            }
            else
            {
                Log.Error("Failed to find target patch location");
            }
        }

        static bool EquipmentSlot_PerformEquipmentAction(On.RoR2.EquipmentSlot.orig_PerformEquipmentAction orig, EquipmentSlot self, EquipmentDef equipmentDef)
        {
            bool result = orig(self, equipmentDef);

            try
            {
                if (!result && equipmentDef == DLC1Content.Equipment.MultiShopCard && self.GetCurrentEquipmentActionQualityTier() > QualityTier.None)
                {
                    if (self.characterBody &&
                        self.characterBody.master &&
                        self.characterBody.master.TryGetComponentCached(out CharacterMasterExtraStatsTracker masterExtraStats))
                    {
                        self.UpdateTargets(DLC1Content.Equipment.MultiShopCard.equipmentIndex, false);

                        GameObject targetObject = self.currentTarget.rootObject;
                        if (targetObject && targetObject.TryGetComponent(out InteractableInfoProvider targetInteractable))
                        {
                            StoredInteractableInfo targetInteractableInfo = new StoredInteractableInfo
                            {
                                InteractableIndex = targetInteractable.CatalogIndex
                            };

                            if (targetObject.TryGetComponent(out SummonMasterBehavior summonMasterBehavior))
                            {
                                targetInteractableInfo.UpgradeValue = summonMasterBehavior.droneUpgradeCount;
                            }

                            if (targetInteractableInfo != masterExtraStats.CardStoredInteractableInfo)
                            {
                                masterExtraStats.CardStoredInteractableInfo = targetInteractableInfo;

                                PointSoundManager.EmitSoundServer(ItemQualitiesContent.NetworkSoundEvents.DuplicateInteractable.index, targetInteractable.IndicatorTransform.position);

                                Log.Debug($"Stored {InteractableCatalog.GetInteractableDef(targetInteractable.CatalogIndex)} in {Util.GetBestMasterName(self.characterBody.master)}");

                                result = true;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning_NoCallerPrefix(e);
            }

            return result;
        }

        static void EquipmentIcon_SetDisplayData(ILContext il)
        {
            if (!il.Method.TryFindParameter<EquipmentIcon.DisplayData>(out ParameterDefinition displayDataParameter))
            {
                Log.Error("Failed to find DisplayData parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchStfld<TooltipProvider>(nameof(TooltipProvider.bodyColor))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, displayDataParameter);
            c.EmitDelegate<Action<EquipmentIcon, EquipmentIcon.DisplayData>>(setupTooltipProvider);

            static void setupTooltipProvider(EquipmentIcon equipmentIcon, EquipmentIcon.DisplayData displayData)
            {
                if (!equipmentIcon || !equipmentIcon.tooltipProvider)
                    return;

                bool shouldDisplayCardTooltip = false;

                EquipmentIndex equipmentIndex = displayData.equipmentDef ? displayData.equipmentDef.equipmentIndex : EquipmentIndex.None;
                if (equipmentIndex != EquipmentIndex.None)
                {
                    if (QualityCatalog.GetQualityTier(equipmentIndex) != QualityTier.None &&
                        QualityCatalog.FindEquipmentQualityGroupIndex(equipmentIndex) == ItemQualitiesContent.EquipmentQualityGroups.MultiShopCard.GroupIndex)
                    {
                        Inventory inventory = equipmentIcon.targetInventory;
                        CharacterMasterExtraStatsTracker masterExtraStats = inventory ? inventory.GetComponentCached<CharacterMasterExtraStatsTracker>() : null;
                        if (masterExtraStats.CardStoredInteractableInfo.InteractableIndex != -1)
                        {
                            shouldDisplayCardTooltip = true;
                        }
                    }
                }

                bool hasTooltipExtraContent = equipmentIcon.tooltipProvider.extraUIDisplayPrefab;
                if (shouldDisplayCardTooltip != hasTooltipExtraContent)
                {
                    if (shouldDisplayCardTooltip)
                    {
                        equipmentIcon.tooltipProvider.extraUIDisplayPrefab = ItemQualitiesContent.Prefabs.MultiShopCardTooltipContext;
                    }
                    else if (equipmentIcon.tooltipProvider.extraUIDisplayPrefab == ItemQualitiesContent.Prefabs.MultiShopCardTooltipContext)
                    {
                        equipmentIcon.tooltipProvider.extraUIDisplayPrefab = null;
                    }
                }
            }
        }
    }
}
