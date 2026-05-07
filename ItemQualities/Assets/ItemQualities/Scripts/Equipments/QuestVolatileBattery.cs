using EntityStates.QuestVolatileBattery;
using EntityStates.QuestVolatileBatteryQuality;
using HG;
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
        static EffectIndex _explosionEffectIndex = EffectIndex.Invalid;

        static GameObject _qualityVolatileBatteryAttachmentPrefab;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        static void Init()
        {
            IL.RoR2.EquipmentSlot.UpdateTargets += EquipmentSlot_UpdateTargets;
            On.RoR2.EquipmentSlot.PerformEquipmentAction += EquipmentSlot_PerformEquipmentAction;
            On.RoR2.GenericPickupController.Start += GenericPickupController_Start;
            On.RoR2.GenericPickupController.OnInteractionBegin += GenericPickupController_OnInteractionBegin;

            _explosionEffectIndex = EffectCatalogUtils.FindEffectIndex("VolatileBatteryExplosion");
            if (_explosionEffectIndex == EffectIndex.Invalid)
            {
                Log.Error("Failed to find VolatileBatteryExplosion effect index");
            }
        }

        [ContentInitializer]
        static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> questVolatileBatteryAttachmentLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_QuestVolatileBattery.QuestVolatileBatteryAttachment_prefab);
            questVolatileBatteryAttachmentLoad.OnSuccess(questVolatileBatteryAttachmentPrefab =>
            {
                _qualityVolatileBatteryAttachmentPrefab = questVolatileBatteryAttachmentPrefab.InstantiateClone("QualityVolatileBatteryAttachment", true);

                _qualityVolatileBatteryAttachmentPrefab.EnsureComponent<QualityTierContext>();
                _qualityVolatileBatteryAttachmentPrefab.EnsureComponent<GenericOwnership>();

                EntityStateMachine stateMachine = _qualityVolatileBatteryAttachmentPrefab.GetComponent<EntityStateMachine>();
                stateMachine.initialStateType = new EntityStates.SerializableEntityStateType(typeof(QuestVolatileBatteryQualityMonitor));
                stateMachine.mainStateType = new EntityStates.SerializableEntityStateType(typeof(QuestVolatileBatteryQualityMonitor));

                args.ContentPack.networkedObjectPrefabs.Add(_qualityVolatileBatteryAttachmentPrefab);
            });

            return questVolatileBatteryAttachmentLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        private static void GenericPickupController_Start(On.RoR2.GenericPickupController.orig_Start orig, GenericPickupController self)
        {
            orig(self);

            if (NetworkServer.active && !self.transform.parent)
            {
                GameObject prefab = GameObject.Instantiate(ItemQualitiesContent.NetworkedPrefabs.QuestVolatileBatteryPickup, self.transform);
                NetworkServer.Spawn(prefab);
            }
        }

        private static void GenericPickupController_OnInteractionBegin(On.RoR2.GenericPickupController.orig_OnInteractionBegin orig, GenericPickupController self, Interactor activator)
        {
            try
            {
                QuestVolatileBatteryPickup questVolatileBatteryPickup = self.GetComponentInChildren<QuestVolatileBatteryPickup>();
                if (questVolatileBatteryPickup)
                {
                    questVolatileBatteryPickup.OnInteractionBegin(activator);
                }
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e.ToString());
            }

            orig(self, activator);
        }

        static void EquipmentSlot_UpdateTargets(ILContext il)
        {
            if (!il.Method.TryFindParameter<EquipmentIndex>("targetingEquipmentIndex", out ParameterDefinition targetingEquipmentIndexParameter))
            {
                Log.Error("Failed to find 'targetingEquipmentIndex' parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            ILLabel targetEnemyLabel = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdarg(targetingEquipmentIndexParameter),
                               x => x.MatchLdsfld(typeof(RoR2Content.Equipment), nameof(RoR2Content.Equipment.Lightning)),
                               x => x.MatchCallOrCallvirt<EquipmentDef>("get_" + nameof(EquipmentDef.equipmentIndex)),
                               x => x.MatchBeq(out targetEnemyLabel)))
            {
                Log.Error("Failed to find target patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, targetingEquipmentIndexParameter);
            c.EmitDelegate<Func<EquipmentSlot, EquipmentIndex, bool>>(shouldTargetEnemy);
            c.Emit(OpCodes.Brtrue, targetEnemyLabel);

            bool shouldTargetEnemy(EquipmentSlot equipmentSlot, EquipmentIndex targetingEquipmentIndex)
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
            if (orig(self, equipmentDef))
                return true;

            if (equipmentDef == RoR2Content.Equipment.QuestVolatileBattery)
            {
                QualityTier qualityTier = self.GetCurrentEquipmentActionQualityTier();

                GameObject targetObject = self.currentTarget.rootObject;
                if (qualityTier != QualityTier.None && targetObject)
                {
                    self.UpdateTargets(RoR2Content.Equipment.QuestVolatileBattery.equipmentIndex, false);

                    GameObject volatileBatteryAttachment = GameObject.Instantiate(_qualityVolatileBatteryAttachmentPrefab);

                    volatileBatteryAttachment.GetComponent<QualityTierContext>().QualityTier = qualityTier;

                    volatileBatteryAttachment.GetComponent<GenericOwnership>().ownerObject = self.gameObject;

                    volatileBatteryAttachment.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(targetObject);

                    return true;
                }
            }

            return false;
        }

        public static void Detonate(GameObject victimObject, float damageMultiplier = 1f)
        {
            if (!NetworkServer.active)
                return;

            QualityTierContext qualityTierContext = victimObject.GetComponent<QualityTierContext>();
            if (!qualityTierContext || qualityTierContext.QualityTier <= QualityTier.None)
                return;

            CharacterBody ownerBody = null;
            if (victimObject.TryGetComponent(out GenericOwnership ownership) && ownership.ownerObject)
            {
                ownerBody = ownership.ownerObject.GetComponent<CharacterBody>();
            }

            CharacterBody victimBody = victimObject.GetComponent<CharacterBody>();
            GenericPickupController victimPickupController = victimObject.GetComponentInParent<GenericPickupController>();

            float damageCoefficient = qualityTierContext.QualityTier switch
            {
                QualityTier.Uncommon => 20f,
                QualityTier.Rare => 30f,
                QualityTier.Epic => 40f,
                QualityTier.Legendary => 50f,
                _ => 0f
            };

            damageCoefficient *= damageMultiplier;

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

            EffectManager.SpawnEffect(_explosionEffectIndex, new EffectData
            {
                origin = explosionPosition,
                scale = explosionRadius,
            }, transmit: true);

            BlastAttack blastAttack = new BlastAttack();
            blastAttack.position = explosionPosition + UnityEngine.Random.onUnitSphere;
            blastAttack.falloffModel = BlastAttack.FalloffModel.None;
            blastAttack.attacker = victimObject;
            blastAttack.inflictor = victimObject;
            blastAttack.radius = explosionRadius;

            if (ownerBody)
            {
                blastAttack.attacker = ownerBody.gameObject;
                blastAttack.baseDamage = ownerBody.damage * damageCoefficient;
                blastAttack.teamIndex = ownerBody.teamComponent.teamIndex;
                blastAttack.crit = ownerBody.RollCrit();
            }
            else
            {
                blastAttack.baseDamage = Run.instance.teamlessDamageCoefficient * damageCoefficient;
                blastAttack.crit = false;
            }

            blastAttack.damageColorIndex = DamageColorIndex.Item;
            blastAttack.baseForce = 5000f;
            blastAttack.bonusForce = Vector3.zero;
            blastAttack.attackerFiltering = AttackerFiltering.AlwaysHit;
            blastAttack.procChainMask = new ProcChainMask();
            blastAttack.procCoefficient = 1f;
            blastAttack.Fire();
        }
    }
}
