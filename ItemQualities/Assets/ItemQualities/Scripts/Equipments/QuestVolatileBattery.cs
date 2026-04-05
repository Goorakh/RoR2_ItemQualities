using EntityStates.QuestVolatileBattery;
using HG;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Items;
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
        static GameObject _explosionEffectPrefab;

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.EquipmentSlot.UpdateTargets += EquipmentSlot_UpdateTargets;
            On.RoR2.EquipmentSlot.PerformEquipmentAction += EquipmentSlot_PerformEquipmentAction;
            On.RoR2.GenericPickupController.Start += GenericPickupController_Start;

            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_QuestVolatileBattery.VolatileBatteryExplosion_prefab).OnSuccess(VolatileBatteryExplosion =>
            {
                _explosionEffectPrefab = VolatileBatteryExplosion;
            });
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

        private static void GenericPickupController_Start(On.RoR2.GenericPickupController.orig_Start orig, GenericPickupController self)
        {
            if (!self.transform.parent && NetworkServer.active)
            {
                if (!self.transform.Find("QuestVolatileBatteryPickup(Clone)"))
                {
                    GameObject prefab = GameObject.Instantiate(ItemQualitiesContent.NetworkedPrefabs.QuestVolatileBatteryPickup, self.transform);
                    NetworkServer.Spawn(prefab);
                }
            }
        }

        private static void GenericPickupController_OnInteractionBegin(On.RoR2.GenericPickupController.orig_OnInteractionBegin orig, GenericPickupController self, Interactor activator)
        {
            orig(self, activator);
            Transform questVolatileBatteryPickup = self.transform.Find("QuestVolatileBatteryPickup(Clone)");
            if (questVolatileBatteryPickup)
            {
                if (questVolatileBatteryPickup.TryGetComponent<GenericOwnership>(out GenericOwnership ownership))
                {
                    ownership.ownerObject = activator.gameObject;
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

        public static void Detonate(GameObject victimObject, bool isPickup)
        {
            if (!NetworkServer.active)
                return;

            QualityTierContext qualityTierContext = victimObject.GetComponent<QualityTierContext>();
            if (!qualityTierContext || qualityTierContext.QualityTier <= QualityTier.None)
                return;

            CharacterBody ownerBody = null;
            GenericOwnership ownership = victimObject.GetComponent<GenericOwnership>();
            if (ownership && ownership.ownerObject)
            {
                ownerBody = ownership.ownerObject.GetComponent<CharacterBody>();
            }

            CharacterBody victimBody = victimObject.GetComponent<CharacterBody>();
            GenericPickupController victimPickupController = victimObject.GetComponent<GenericPickupController>();

            float damageMul = qualityTierContext.QualityTier switch
            {
                QualityTier.Uncommon => 20,
                QualityTier.Rare => 30,
                QualityTier.Epic => 40,
                QualityTier.Legendary => 50,
                _ => 0
            };

            if (isPickup)
            {
                damageMul *= 10;
            }

            Vector3 explosionPosition = victimObject.transform.position;
            if (victimBody)
            {
                explosionPosition = victimBody.corePosition;
            }
            else if (victimPickupController && victimPickupController.pickupDisplay)
            {
                explosionPosition = victimPickupController.pickupDisplay.transform.position;
            }

            float explosionRadius = ExplodeOnDeath.GetExplosionRadius(30f, ownerBody);

            EffectManager.SpawnEffect(_explosionEffectPrefab, new EffectData
            {
                origin = explosionPosition,
                scale = explosionRadius,
            }, transmit: true);

            BlastAttack blastAttack = new BlastAttack();
            blastAttack.position = explosionPosition + UnityEngine.Random.onUnitSphere;
            blastAttack.falloffModel = BlastAttack.FalloffModel.None;
            if (ownerBody)
            {
                blastAttack.attacker = ownerBody.gameObject;
                blastAttack.inflictor = ownerBody.gameObject;
                blastAttack.baseDamage = ownerBody.damage * damageMul;
                blastAttack.teamIndex = ownerBody.teamComponent.teamIndex;
                blastAttack.radius = explosionRadius;
                blastAttack.crit = ownerBody.RollCrit();
            }
            else
            {
                blastAttack.baseDamage = (Run.instance.ambientLevelFloor * 2 + 10) * damageMul;
                blastAttack.radius = explosionRadius;
                blastAttack.crit = false;
            }
            blastAttack.damageColorIndex = DamageColorIndex.Item;
            blastAttack.baseForce = 5000f;
            blastAttack.bonusForce = Vector3.zero;
            blastAttack.attackerFiltering = AttackerFiltering.AlwaysHit;
            blastAttack.procChainMask = default(ProcChainMask);
            blastAttack.procCoefficient = 1f;
            blastAttack.Fire();
            if (isPickup)
            {
                GameObject.Destroy(victimObject.transform.parent.gameObject);
            } else {
                GameObject.Destroy(victimObject);
            }
        }
    }
}
