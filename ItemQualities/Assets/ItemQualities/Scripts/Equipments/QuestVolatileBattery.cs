using EntityStates.QuestVolatileBattery;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Equipments
{
    static class QuestVolatileBattery
    {
        static GameObject _qualityVolatileBatteryAttachment;

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.EquipmentSlot.UpdateTargets += EquipmentSlot_UpdateTargets;
            On.RoR2.EquipmentSlot.PerformEquipmentAction += EquipmentSlot_PerformEquipmentAction;
            On.RoR2.GenericPickupController.OnInteractionBegin += GenericPickupController_OnInteractionBegin;
            On.RoR2.GenericPickupController.OnEnable += GenericPickupController_OnEnable;
        }

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            ParallelProgressCoroutine coroutine = new ParallelProgressCoroutine(args.ProgressReceiver);

            AsyncOperationHandle<GameObject> QuestVolatileBatteryAttachmentLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_QuestVolatileBattery.QuestVolatileBatteryAttachment_prefab);
            QuestVolatileBatteryAttachmentLoad.OnSuccess(QuestVolatileBatteryAttachmentPrefab =>
            {
                _qualityVolatileBatteryAttachment = QuestVolatileBatteryAttachmentPrefab.InstantiateClone("QualityVolatileBatteryAttachment", true);
                _qualityVolatileBatteryAttachment.AddComponent<QualityTierContext>();
                _qualityVolatileBatteryAttachment.AddComponent<GenericOwnership>();
                args.ContentPack.networkedObjectPrefabs.Add(_qualityVolatileBatteryAttachment);
            });
            coroutine.Add(QuestVolatileBatteryAttachmentLoad);

            return coroutine;
        }

        private static void GenericPickupController_OnEnable(On.RoR2.GenericPickupController.orig_OnEnable orig, GenericPickupController self)
        {
            orig(self);
            if (!self.transform.parent)
            {
                pickupAddExplosion(self.pickup.pickupIndex, self.transform);
            }
        }

        private static void GenericPickupController_OnInteractionBegin(On.RoR2.GenericPickupController.orig_OnInteractionBegin orig, GenericPickupController self, Interactor activator)
        {
            orig(self, activator);
            pickupAddExplosion(self.pickup.pickupIndex, self.transform, activator.gameObject);
        }

        static void pickupAddExplosion(PickupIndex pickupIndex, Transform parent, GameObject owner = null)
        {
            if (NetworkServer.active)
            {
                EquipmentIndex equipmentIndex = PickupCatalog.GetPickupDef(pickupIndex)?.equipmentIndex ?? EquipmentIndex.None;
                EquipmentQualityGroupIndex equipmentGroup = QualityCatalog.FindEquipmentQualityGroupIndex(equipmentIndex);
                if (QualityCatalog.GetQualityTier(equipmentIndex) > QualityTier.None &&
                equipmentGroup == ItemQualitiesContent.EquipmentQualityGroups.QuestVolatileBattery.GroupIndex)
                {
                    GameObject prefab = GameObject.Instantiate(ItemQualitiesContent.NetworkedPrefabs.QuestVolatileBatteryPickup, parent);
                    prefab.GetComponent<QualityTierContext>().QualityTier = QualityCatalog.GetQualityTier(equipmentIndex);
                    prefab.GetComponent<GenericOwnership>().ownerObject = owner;
                    NetworkServer.Spawn(prefab);
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
            ILLabel label = null;
            if (c.TryGotoNext(MoveType.After,
                              x => x.MatchLdarg(1),
                              x => x.MatchLdsfld(typeof(RoR2Content.Equipment), nameof(RoR2Content.Equipment.Lightning)),
                              x => x.MatchCallOrCallvirt(typeof(EquipmentDef), "get_equipmentIndex"),
                              x => x.MatchBeq(out label)))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldarg, targetingEquipmentIndexParameter);
                c.EmitDelegate<Func<EquipmentSlot, EquipmentIndex, bool>>(addEnemyTargeting);
                c.Emit(OpCodes.Brtrue, label);
            }
            else
            {
                Log.Error("Failed to find target patch location");
            }

            bool addEnemyTargeting(EquipmentSlot equipmentSlot, EquipmentIndex targetingEquipmentIndex)
            {
                if (equipmentSlot.GetActiveEquipmentQualityTier() == QualityTier.None)
                    return false;
                EquipmentQualityGroupIndex targetingEquipmentGroup = QualityCatalog.FindEquipmentQualityGroupIndex(targetingEquipmentIndex);
                if (targetingEquipmentGroup == ItemQualitiesContent.EquipmentQualityGroups.QuestVolatileBattery.GroupIndex)
                {
                    return true;
                }
                return false;
            }
        }

        static bool EquipmentSlot_PerformEquipmentAction(On.RoR2.EquipmentSlot.orig_PerformEquipmentAction orig, EquipmentSlot self, EquipmentDef equipmentDef)
        {
            bool result = orig(self, equipmentDef);

            if (!result && equipmentDef == RoR2Content.Equipment.QuestVolatileBattery && self.GetCurrentEquipmentActionQualityTier() > QualityTier.None)
            {
                GameObject targetObject = self.currentTarget.rootObject;
                if (targetObject)
                {
                    if (NetworkServer.active)
                    {
                        self.UpdateTargets(RoR2Content.Equipment.QuestVolatileBattery.equipmentIndex, false);
                        GameObject prefab = GameObject.Instantiate(_qualityVolatileBatteryAttachment);
                        NetworkedBodyAttachment bodyAttachment = prefab.GetComponent<NetworkedBodyAttachment>();
                        bodyAttachment.AttachToGameObjectAndSpawn(targetObject);
                        QualityTierContext qualityTierContext = prefab.GetComponent<QualityTierContext>();
                        qualityTierContext.QualityTier = self.GetCurrentEquipmentActionQualityTier();
                        prefab.GetComponent<GenericOwnership>().ownerObject = self.gameObject;
                        prefab.GetComponent<EntityStateMachine>().SetState(new QuestVolatileBatteryQualityMonitor());
                    }
                    
                    result = true;
                }
            }

            return result;
        }
    }
}
