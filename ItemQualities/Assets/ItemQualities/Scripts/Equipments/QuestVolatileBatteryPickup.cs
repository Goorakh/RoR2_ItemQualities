using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public class QuestVolatileBatteryPickup : NetworkBehaviour
    {
        GenericPickupController _pickupController;
        QualityTierContext _qualityTierContext;

        static GameObject _vfxPrefab;
        GameObject _vfxInstance;
        float _timer;

        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_QuestVolatileBattery.VolatileBatteryPreDetonation_prefab).OnSuccess(VolatileBatteryPreDetonation =>
            {
                _vfxPrefab = VolatileBatteryPreDetonation;
            });
        }

        bool Begin()
        {
            if (!transform.parent)
                return false;
            if (_pickupController && _qualityTierContext)
            {
                return true;
            }
            _pickupController = transform.parent.GetComponent<GenericPickupController>();
            if (!_pickupController || !_pickupController.pickup.isValid)
                return false;
            _qualityTierContext = GetComponent<QualityTierContext>();
            if (!_qualityTierContext)
                return false;
            return true;
        }

        private void FixedUpdate()
        {
            if (!Begin())
                return;
            _timer += Time.deltaTime;

            if (!_vfxInstance)
            {
                _timer = 0;
                EquipmentIndex equipmentIndex = PickupCatalog.GetPickupDef(_pickupController.pickup.pickupIndex)?.equipmentIndex ?? EquipmentIndex.None;
                if (QualityCatalog.FindEquipmentQualityGroupIndex(equipmentIndex) == ItemQualitiesContent.EquipmentQualityGroups.QuestVolatileBattery.GroupIndex &&
                    QualityCatalog.GetQualityTier(equipmentIndex) > QualityTier.None)
                {
                    GetComponent<QualityTierContext>().QualityTier = QualityCatalog.GetQualityTier(equipmentIndex);
                    GameObject displayParent = _pickupController.pickupDisplay.modelRenderer.gameObject;
                    _vfxInstance = UnityEngine.Object.Instantiate(_vfxPrefab, displayParent.transform);
                    _vfxInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                }
            }

            if (_timer >= 3f)
            {
                Equipments.QuestVolatileBattery.Detonate(base.gameObject, true);
            }
        }
    }
}
