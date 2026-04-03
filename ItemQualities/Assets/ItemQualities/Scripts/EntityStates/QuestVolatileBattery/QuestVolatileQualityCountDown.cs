using ItemQualities;
using ItemQualities.Equipments;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace EntityStates.QuestVolatileBattery
{
    public class QuestVolatileBatteryQualityCountDown : QuestVolatileBatteryBaseState
    {
        public static float duration;
        public static float explosionRadius;

        protected GameObject _vfxInstance;

        [NonSerialized]
        public static GameObject vfxPrefab;
        [NonSerialized]
        public static GameObject explosionEffectPrefab;

        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_QuestVolatileBattery.VolatileBatteryPreDetonation_prefab).OnSuccess(VolatileBatteryPreDetonation =>
            {
                vfxPrefab = VolatileBatteryPreDetonation;
            });
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_QuestVolatileBattery.VolatileBatteryExplosion_prefab).OnSuccess(VolatileBatteryExplosion =>
            {
                explosionEffectPrefab = VolatileBatteryExplosion;
            });
        }

        public override void OnEnter()
        {
            base.OnEnter();
            if (!vfxPrefab)
            {
                return;
            }

            GameObject gameObject = UnityEngine.Object.Instantiate(vfxPrefab, networkedBodyAttachment.attachedBody.transform);
            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = Vector3.one * networkedBodyAttachment.attachedBody.bestFitActualRadius;
            _vfxInstance = gameObject;
        }

        public override void OnExit()
        {
            if (_vfxInstance)
            {
                EntityState.Destroy(_vfxInstance);
            }
            base.OnExit();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (NetworkServer.active)
            {
                FixedUpdateServer();
            }
        }

        private void FixedUpdateServer()
        {
            if (base.fixedAge >= duration)
            {
                Detonate();
            }
        }

        public void Detonate()
        {
            if ((bool)base.networkedBodyAttachment.attachedBody)
            {
                QualityCheck qualityCheck = GetComponent<QualityCheck>();
                if (!qualityCheck || qualityCheck.qualityTier <= QualityTier.None)
                    return;
                GenericOwnership ownership = GetComponent<GenericOwnership>();
                if (!ownership || !ownership.ownerObject)
                    return;
                CharacterBody ownerBody = ownership.ownerObject.GetComponent<CharacterBody>();
                if (!ownerBody)
                    return;

                Vector3 corePosition = base.networkedBodyAttachment.attachedBody.corePosition;
                float damageMul = qualityCheck.qualityTier switch
                {
                    QualityTier.Uncommon => 20,
                    QualityTier.Rare => 30,
                    QualityTier.Epic => 40,
                    QualityTier.Legendary => 50,
                    _ => 0
                };
                EffectManager.SpawnEffect(explosionEffectPrefab, new EffectData
                {
                    origin = corePosition,
                    scale = explosionRadius
                }, transmit: true);

                BlastAttack blastAttack = new BlastAttack();
                blastAttack.position = corePosition + UnityEngine.Random.onUnitSphere;
                blastAttack.radius = explosionRadius;
                blastAttack.falloffModel = BlastAttack.FalloffModel.None;
                blastAttack.attacker = ownerBody.gameObject;
                blastAttack.inflictor = ownerBody.gameObject;
                blastAttack.damageColorIndex = DamageColorIndex.Item;
                blastAttack.baseDamage = ownerBody.baseDamage * damageMul;
                blastAttack.baseForce = 5000f;
                blastAttack.bonusForce = Vector3.zero;
                blastAttack.attackerFiltering = AttackerFiltering.AlwaysHit;
                blastAttack.crit = false;
                blastAttack.procChainMask = default(ProcChainMask);
                blastAttack.procCoefficient = 1f;
                blastAttack.teamIndex = ownerBody.teamComponent.teamIndex;
                blastAttack.Fire();
                GameObject.Destroy(base.gameObject);
            }
        }
    }

    public class QuestVolatileBatteryPickup : QuestVolatileBatteryQualityCountDown
    {
        GenericPickupController _pickupController;

        public override void OnEnter()
        {
            base.OnEnter();
            _pickupController = transform.parent.GetComponent<GenericPickupController>();
            GameObject instance = _pickupController.pickupDisplay.modelRenderer.gameObject;
            _vfxInstance = UnityEngine.Object.Instantiate(vfxPrefab, instance.transform);
            _vfxInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!_pickupController || !_pickupController.pickup.isValid)
                return;
            EquipmentIndex equipmentIndex = PickupCatalog.GetPickupDef(_pickupController.pickup.pickupIndex)?.equipmentIndex ?? EquipmentIndex.None;
            if (QualityCatalog.FindEquipmentQualityGroupIndex(equipmentIndex) != ItemQualitiesContent.EquipmentQualityGroups.QuestVolatileBattery.GroupIndex ||
                QualityCatalog.GetQualityTier(equipmentIndex) <= QualityTier.None)
            {
                GameObject.Destroy(base.gameObject);
            }
        }
    }
}
