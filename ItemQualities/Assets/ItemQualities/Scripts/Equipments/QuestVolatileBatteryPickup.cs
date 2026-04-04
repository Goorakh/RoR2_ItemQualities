using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public class QuestVolatileBatteryPickup : MonoBehaviour
    {
        GenericPickupController _pickupController;

        static GameObject _vfxPrefab;
        static GameObject _explosionEffectPrefab;
        GameObject _vfxInstance;
        float _timer;

        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_QuestVolatileBattery.VolatileBatteryPreDetonation_prefab).OnSuccess(VolatileBatteryPreDetonation =>
            {
                _vfxPrefab = VolatileBatteryPreDetonation;
            });
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_QuestVolatileBattery.VolatileBatteryExplosion_prefab).OnSuccess(VolatileBatteryExplosion =>
            {
                _explosionEffectPrefab = VolatileBatteryExplosion;
            });
        }

        void Begin()
        {
            if (!transform.parent)
                return;
            _pickupController = transform.parent.GetComponent<GenericPickupController>();
            if (_pickupController)
            {
                GameObject instance = _pickupController.pickupDisplay.modelRenderer.gameObject;
                _vfxInstance = UnityEngine.Object.Instantiate(_vfxPrefab, instance.transform);
                _vfxInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
        }

        private void FixedUpdate()
        {
            _timer += Time.deltaTime;
            if (!_pickupController)
            {
                Begin();
            }
            if (!_pickupController || !_pickupController.pickup.isValid)
                return;
            EquipmentIndex equipmentIndex = PickupCatalog.GetPickupDef(_pickupController.pickup.pickupIndex)?.equipmentIndex ?? EquipmentIndex.None;
            if (QualityCatalog.FindEquipmentQualityGroupIndex(equipmentIndex) != ItemQualitiesContent.EquipmentQualityGroups.QuestVolatileBattery.GroupIndex ||
                QualityCatalog.GetQualityTier(equipmentIndex) <= QualityTier.None)
            {
                GameObject.Destroy(base.gameObject);
            }

            if (_timer >= 3f)
            {
                Detonate();
            }
        }

        public void Detonate()
        {
            if (!NetworkServer.active) 
                return;
            QualityTierContext qualityTierContext = GetComponent<QualityTierContext>();
            if (!qualityTierContext || qualityTierContext.QualityTier <= QualityTier.None)
                return;
            CharacterBody ownerBody = null;
            GenericOwnership ownership = GetComponent<GenericOwnership>();
            if (ownership && ownership.ownerObject)
            {
                ownerBody = ownership.ownerObject.GetComponent<CharacterBody>();
            }

            Vector3 corePosition = Vector3.zero;
            corePosition = base.transform.position;
            float damageMul = qualityTierContext.QualityTier switch
            {
                QualityTier.Uncommon => 20,
                QualityTier.Rare => 30,
                QualityTier.Epic => 40,
                QualityTier.Legendary => 50,
                _ => 0
            };
            damageMul *= 10;
            EffectManager.SpawnEffect(_explosionEffectPrefab, new EffectData
            {
                origin = corePosition,
                scale = 30
            }, transmit: true);

            BlastAttack blastAttack = new BlastAttack();
            blastAttack.position = corePosition + UnityEngine.Random.onUnitSphere;
            blastAttack.radius = 30;
            blastAttack.falloffModel = BlastAttack.FalloffModel.None;
            if (ownerBody)
            {
                blastAttack.attacker = ownerBody.gameObject;
                blastAttack.inflictor = ownerBody.gameObject;
                blastAttack.baseDamage = ownerBody.damage * damageMul;
                blastAttack.teamIndex = ownerBody.teamComponent.teamIndex;
            } else {
                blastAttack.baseDamage = (Run.instance.ambientLevelFloor * 2 + 10) * damageMul;
            }
            blastAttack.damageColorIndex = DamageColorIndex.Item;
            blastAttack.baseForce = 5000f;
            blastAttack.bonusForce = Vector3.zero;
            blastAttack.attackerFiltering = AttackerFiltering.AlwaysHit;
            blastAttack.crit = false;
            blastAttack.procChainMask = default(ProcChainMask);
            blastAttack.procCoefficient = 1f;
            blastAttack.Fire();
            GameObject.Destroy(base.transform.parent.gameObject);
        }
    }
}
