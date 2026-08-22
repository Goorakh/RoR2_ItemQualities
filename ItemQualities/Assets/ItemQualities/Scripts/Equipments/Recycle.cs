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
    internal static class Recycle
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.EquipmentSlot.UpdateTargets += EquipmentSlot_UpdateTargets;
            IL.RoR2.EquipmentSlot.FireRecycle += IL_EquipmentSlot_FireRecycle;
            On.RoR2.EquipmentSlot.FireRecycle += On_EquipmentSlot_FireRecycle;
        }

        private static void EquipmentSlot_UpdateTargets(ILContext il)
        {
            if (!il.Method.TryFindParameter<EquipmentIndex>(out ParameterDefinition targetingEquipmentIndexParameter))
            {
                targetingEquipmentIndexParameter = null;
                Log.Warning("Failed to find targetingEquipmentIndex parameter");
            }

            ILCursor c = new ILCursor(il);

            // RecyclableObject target search
            {
                c.Goto(0);

                if (!c.TryGotoNext(MoveType.After,
                                   x => x.MatchCallOrCallvirt<EquipmentSlot>(nameof(EquipmentSlot.FindPickupController)),
                                   x => x.MatchNewobj<EquipmentSlot.UserTargetInfo>(),
                                   x => x.MatchStfld<EquipmentSlot>(nameof(EquipmentSlot.currentTarget))))
                {
                    Log.Error("Failed to find target search patch location");
                }
                else if (targetingEquipmentIndexParameter != null)
                {
                    c.Emit(OpCodes.Ldarg_0);
                    c.Emit(OpCodes.Ldarg, targetingEquipmentIndexParameter);
                    c.EmitDelegate<Action<EquipmentSlot, EquipmentIndex>>(tryModifyCurrentPickupTarget);

                    static void tryModifyCurrentPickupTarget(EquipmentSlot equipmentSlot, EquipmentIndex targetingEquipmentIndex)
                    {
                        if (targetingEquipmentIndex != RoR2Content.Equipment.Recycle.equipmentIndex)
                            return;

                        if (equipmentSlot.GetActiveEquipmentQualityTier() == QualityTier.None)
                            return;

                        // If we already have a target, don't search for recyclable object
                        if (equipmentSlot.currentTarget.rootObject)
                            return;

                        CharacterBody body = equipmentSlot.characterBody;
                        if (!body)
                            return;

                        InputBankTest inputBank = body.inputBank;
                        if (!inputBank)
                            return;

                        Ray aimRay = inputBank.GetAimRay();

                        RecyclableObjectSearch recyclableObjectSearch = RecyclableObjectSearch.SharedInstance;

                        // Values copied from EquipmentSlot.FindPickupController
                        aimRay = CameraRigController.ModifyAimRayIfApplicable(aimRay, equipmentSlot.gameObject, out float extraRaycastDistance);
                        recyclableObjectSearch.searchOrigin = aimRay.origin;
                        recyclableObjectSearch.searchDirection = aimRay.direction;
                        recyclableObjectSearch.minAngleFilter = 0f;
                        recyclableObjectSearch.maxAngleFilter = 10f;
                        recyclableObjectSearch.minDistanceFilter = 0f;
                        recyclableObjectSearch.maxDistanceFilter = 30f + extraRaycastDistance;
                        recyclableObjectSearch.filterByDistinctEntity = true;
                        recyclableObjectSearch.filterByLoS = true;
                        recyclableObjectSearch.sortMode = SortMode.Angle;

                        // Don't filter out already recycled objects, since we want to show the invalid indicator on them
                        recyclableObjectSearch.RequireRecyclable = false;

                        RecyclableObject targetObject = recyclableObjectSearch.SearchCandidatesForSingleTarget(InstanceTracker.GetInstancesList<RecyclableObject>());
                        if (targetObject)
                        {
                            equipmentSlot.currentTarget = new EquipmentSlot.UserTargetInfo
                            {
                                rootObject = targetObject.gameObject,
                                transformToIndicateAt = targetObject.IndicatorTransform,
                            };
                        }
                    }

                    Instruction recyclerValidInstr = null;
                    ILLabel recyclerInvalidLabel = null;
                    if (c.TryGotoNext(MoveType.AfterLabel,
                                      x => x.MatchLdloc(il, out _), // target pickupController
                                      x => x.MatchLdfld<GenericPickupController>(nameof(GenericPickupController.Recycled)),
                                      x => x.MatchBrtrue(out recyclerInvalidLabel),
                                      x => x.MatchAny(out recyclerValidInstr)))
                    {
                        VariableDefinition targetObjectIsRecyclableVar = il.AddVariable<bool>();

                        c.Emit(OpCodes.Ldarg_0);
                        c.Emit(OpCodes.Ldarg, targetingEquipmentIndexParameter);
                        c.Emit(OpCodes.Ldloca, targetObjectIsRecyclableVar);
                        c.EmitDelegate<TryCheckTargetRecyclableObjectIsRecyclableDelegate>(tryCheckTargetRecyclableObjectIsRecyclable);
                        static bool tryCheckTargetRecyclableObjectIsRecyclable(EquipmentSlot equipmentSlot, EquipmentIndex equipmentIndex, out bool isTargetObjectRecyclable)
                        {
                            if (equipmentSlot.currentTarget.rootObject &&
                                equipmentSlot.currentTarget.rootObject.TryGetComponent(out RecyclableObject targetRecyclableObject))
                            {
                                isTargetObjectRecyclable = targetRecyclableObject.IsRecyclable;
                                return true;
                            }

                            isTargetObjectRecyclable = default;
                            return false;
                        }

                        ILLabel defaultRecycleCheckLabel = c.DefineLabel();
                        c.Emit(OpCodes.Brfalse, defaultRecycleCheckLabel);

                        c.Emit(OpCodes.Ldloc, targetObjectIsRecyclableVar);
                        c.Emit(OpCodes.Brtrue, il.DefineLabel(recyclerValidInstr));
                        c.Emit(OpCodes.Br, recyclerInvalidLabel);

                        c.MarkLabel(defaultRecycleCheckLabel);
                    }
                    else
                    {
                        Log.Error("Failed to find targetting indicator patch location");
                    }
                }
            }

            // Quality item recycle filtering
            {
                c.Goto(0);

                int patchCount = 0;

                int pickupControllerVarIndex = -1;
                while (c.TryGotoNext(MoveType.After,
                                     x => x.MatchLdloc(out pickupControllerVarIndex),
                                     x => x.MatchLdfld<GenericPickupController>(nameof(GenericPickupController.Recycled))))
                {
                    c.Emit(OpCodes.Ldloc, pickupControllerVarIndex);
                    c.Emit(OpCodes.Ldarg_0);
                    c.EmitDelegate<Func<bool, GenericPickupController, EquipmentSlot, bool>>(isUnrecyclable);

                    static bool isUnrecyclable(bool isRecycled, GenericPickupController pickupController, EquipmentSlot equipmentSlot)
                    {
                        if (isRecycled)
                            return true;

                        bool pickupIsQuality = pickupController && QualityCatalog.GetQualityTier(pickupController.pickup.pickupIndex) > QualityTier.None;
                        bool recyclerIsQuality = equipmentSlot.GetActiveEquipmentQualityTier() > QualityTier.None;

                        if (pickupIsQuality && !recyclerIsQuality)
                            return true;

                        return false;
                    }

                    patchCount++;
                }

                if (patchCount == 0)
                {
                    Log.Error("Failed to find GenericPickupController.Recycled patch location");
                }
                else
                {
                    Log.Debug($"Found {patchCount} GenericPickupController.Recycled patch location(s)");
                }
            }
        }

        private delegate bool TryCheckTargetRecyclableObjectIsRecyclableDelegate(EquipmentSlot equipmentSlot, EquipmentIndex equipmentIndex, out bool isTargetObjectRecyclable);

        private static void IL_EquipmentSlot_FireRecycle(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            int pickupControllerVarIndex = -1;
            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdloc(out pickupControllerVarIndex),
                                 x => x.MatchLdfld<GenericPickupController>(nameof(GenericPickupController.Recycled))))
            {
                c.Emit(OpCodes.Ldloc, pickupControllerVarIndex);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<bool, GenericPickupController, EquipmentSlot, bool>>(isUnrecyclable);

                static bool isUnrecyclable(bool isRecycled, GenericPickupController pickupController, EquipmentSlot equipmentSlot)
                {
                    if (isRecycled)
                        return true;

                    bool pickupIsQuality = pickupController && QualityCatalog.GetQualityTier(pickupController.pickup.pickupIndex) > QualityTier.None;
                    bool recyclerIsQuality = equipmentSlot.GetCurrentEquipmentActionQualityTier() > QualityTier.None;

                    if (pickupIsQuality && !recyclerIsQuality)
                        return true;

                    return false;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find recyclable patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} recyclable patch location(s)");
            }

            c.Goto(0);

            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchCallOrCallvirt(typeof(PickupTransmutationManager), nameof(PickupTransmutationManager.GetAvailableGroupFromPickupIndex))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<PickupIndex, EquipmentSlot, PickupIndex>>(getPickupIndex);

                static PickupIndex getPickupIndex(PickupIndex pickupIndex, EquipmentSlot equipmentSlot)
                {
                    QualityTier qualityTier = QualityCatalog.GetQualityTier(pickupIndex);
                    QualityTier equipmentQualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();
                    if (qualityTier > equipmentQualityTier)
                    {
                        pickupIndex = QualityCatalog.GetPickupIndexOfQuality(pickupIndex, equipmentQualityTier);
                        qualityTier = equipmentQualityTier;
                    }

                    return pickupIndex;
                }
            }
            else
            {
                Log.Error("Failed to find pickup group patch location");
            }
        }

        private static bool On_EquipmentSlot_FireRecycle(On.RoR2.EquipmentSlot.orig_FireRecycle orig, EquipmentSlot self)
        {
            if (orig(self))
                return true;

            if (self.GetCurrentEquipmentActionQualityTier() > QualityTier.None)
            {
                GameObject targetObject = self.currentTarget.rootObject;
                if (targetObject &&
                    targetObject.TryGetComponent(out RecyclableObject recyclableObject) &&
                    recyclableObject.IsRecyclable)
                {
                    recyclableObject.DoRecycle();
                    self.InvalidateCurrentTarget();
                    return true;
                }
            }

            return false;
        }
    }
}
