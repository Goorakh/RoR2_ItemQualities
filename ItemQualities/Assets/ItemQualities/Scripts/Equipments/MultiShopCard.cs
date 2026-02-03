using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.DirectionalSearch;
using System;
using UnityEngine;

namespace ItemQualities.Equipments
{
    static class MultiShopCard
    {
        static readonly InteractableSearch _sharedInteractableSearch = new InteractableSearch();

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

                        SpecialObjectAttributes targetInteractable = _sharedInteractableSearch.SearchCandidatesForSingleTarget(SpecialObjectAttributes.AllVehiclePassengerAttributes);

                        GameObject targetObject = targetInteractable ? targetInteractable.gameObject : null;

                        Transform targetTransform = null;
                        if (targetInteractable)
                        {
                            targetTransform = targetInteractable.indicatorOffset ? targetInteractable.indicatorOffset : targetInteractable.transform;
                        }

                        equipmentSlot.currentTarget = new EquipmentSlot.UserTargetInfo
                        {
                            rootObject = targetObject,
                            transformToIndicateAt = targetTransform
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
    }
}
