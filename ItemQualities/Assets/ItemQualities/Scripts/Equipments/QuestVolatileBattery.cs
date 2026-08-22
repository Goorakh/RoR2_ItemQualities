using ItemQualities.Items;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Equipments
{
    internal static class QuestVolatileBattery
    {
        private static EffectIndex _explosionEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        private static void Init()
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

        private static void EquipmentSlot_UpdateTargets(ILContext il)
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

            static bool shouldTargetEnemy(EquipmentSlot equipmentSlot, EquipmentIndex targetingEquipmentIndex)
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

        private static bool EquipmentSlot_PerformEquipmentAction(On.RoR2.EquipmentSlot.orig_PerformEquipmentAction orig, EquipmentSlot self, EquipmentDef equipmentDef)
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

                    CharacterBody ownerBody = self.GetComponent<CharacterBody>();

                    GameObject volatileBatteryAttachment = GameObject.Instantiate(ItemQualitiesContent.NetworkedPrefabs.QualityVolatileBatteryAttachment);

                    volatileBatteryAttachment.GetComponentCached<QualityTierContext>().QualityTier = qualityTier;

                    volatileBatteryAttachment.GetComponent<GenericOwnership>().ownerObject = self.gameObject;

                    QuestVolatileBatteryAttachment questVolatileBatteryAttachment = volatileBatteryAttachment.GetComponent<QuestVolatileBatteryAttachment>();
                    questVolatileBatteryAttachment.victimObject = targetObject;
                    questVolatileBatteryAttachment.crit = ownerBody && ownerBody.RollCrit();

                    NetworkServer.Spawn(volatileBatteryAttachment);

                    return true;
                }
            }

            return false;
        }

        public static void Detonate(GameObject inflictor, float damageMultiplier = 1f)
        {
            if (!NetworkServer.active)
                return;

            QualityTier qualityTier = QualityTierContext.GetQualityTier(inflictor);
            if (qualityTier == QualityTier.None)
                return;

            CharacterBody attackerBody = null;
            if (inflictor.TryGetComponent(out GenericOwnership ownership) && ownership.ownerObject)
            {
                attackerBody = ownership.ownerObject.GetComponent<CharacterBody>();
            }

            CharacterBody victimBody = null;
            bool crit = false;
            if (inflictor.TryGetComponent(out QuestVolatileBatteryAttachment questVolatileBatteryAttachment))
            {
                victimBody = questVolatileBatteryAttachment.victimBody;
                crit = questVolatileBatteryAttachment.crit;
            }

            GenericPickupController victimPickupController = inflictor.GetComponentInParent<GenericPickupController>();

            float damageCoefficient = qualityTier switch
            {
                QualityTier.Uncommon => 40f,
                QualityTier.Rare => 50f,
                QualityTier.Epic => 60f,
                QualityTier.Legendary => 70f,
                _ => 0f
            };

            damageCoefficient *= damageMultiplier;

            Vector3 explosionPosition = inflictor.transform.position;
            if (victimBody)
            {
                explosionPosition = victimBody.corePosition;
            }
            else if (victimPickupController && victimPickupController.pickupDisplay)
            {
                explosionPosition = victimPickupController.pickupDisplay.transform.position;
            }

            float explosionRadius = ExplodeOnDeath.GetExplosionRadius(30f, attackerBody);

            EffectManager.SpawnEffect(_explosionEffectIndex, new EffectData
            {
                origin = explosionPosition,
                scale = explosionRadius,
            }, transmit: true);

            BlastAttack blastAttack = new BlastAttack();
            blastAttack.position = explosionPosition + UnityEngine.Random.onUnitSphere;
            blastAttack.falloffModel = BlastAttack.FalloffModel.None;
            blastAttack.inflictor = inflictor;
            blastAttack.radius = explosionRadius;

            if (attackerBody)
            {
                blastAttack.attacker = attackerBody.gameObject;
                blastAttack.baseDamage = attackerBody.damage * damageCoefficient;
                blastAttack.teamIndex = attackerBody.teamComponent.teamIndex;
            }
            else
            {
                blastAttack.baseDamage = Run.instance.teamlessDamageCoefficient * damageCoefficient;
            }

            blastAttack.crit = crit;
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
