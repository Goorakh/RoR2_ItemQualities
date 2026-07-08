using EntityStates;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Equipments
{
    public sealed class QuestVolatileBatteryPickup : MonoBehaviour
    {
        private static GameObject _detonationEffectPrefab;

        private QualityTierContext _qualityTierContext;
        private GenericOwnership _genericOwnership;

        private bool _resolvedParentPickupController;
        private GenericPickupController _pickupController;

        [SystemInitializer]
        private static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_QuestVolatileBattery.VolatileBatteryPreDetonation_prefab).OnSuccess(volatileBatteryPreDetonation =>
            {
                _detonationEffectPrefab = volatileBatteryPreDetonation;
            });
        }

        private void Awake()
        {
            _qualityTierContext = GetComponent<QualityTierContext>();
            _genericOwnership = GetComponent<GenericOwnership>();
        }

        private void FixedUpdate()
        {
            if (!_resolvedParentPickupController && transform.parent)
            {
                _pickupController = transform.parent.GetComponent<GenericPickupController>();
                _resolvedParentPickupController = true;
            }
        }

        public void OnInteractionBegin(Interactor activator)
        {
            _genericOwnership.ownerObject = activator ? activator.gameObject : null;
        }

        private abstract class BaseState : EntityState
        {
            protected QuestVolatileBatteryPickup batteryPickupAttachment { get; private set; }

            protected GenericPickupController pickupController => batteryPickupAttachment._pickupController;

            public override void OnEnter()
            {
                base.OnEnter();

                batteryPickupAttachment = GetComponent<QuestVolatileBatteryPickup>();
            }
        }

        private sealed class Idle : BaseState
        {
            public override void FixedUpdate()
            {
                base.FixedUpdate();

                if (isAuthority)
                {
                    if (pickupController)
                    {
                        PickupDef currentPickupDef = PickupCatalog.GetPickupDef(pickupController.pickup.pickupIndex);
                        EquipmentIndex currentEquipmentIndex = currentPickupDef != null ? currentPickupDef.equipmentIndex : EquipmentIndex.None;
                        EquipmentQualityGroupIndex currentEquipmentGroupIndex = QualityCatalog.FindEquipmentQualityGroupIndex(currentEquipmentIndex);
                        if (currentEquipmentGroupIndex == ItemQualitiesContent.EquipmentQualityGroups.QuestVolatileBattery.GroupIndex &&
                            QualityCatalog.GetQualityTier(currentEquipmentIndex) != QualityTier.None)
                        {
                            outer.SetNextState(new CountDown());
                        }
                    }
                }
            }
        }

        private sealed class CountDown : BaseState
        {
            public static float duration;

            private GameObject _detonationEffectInstance;

            public override void OnEnter()
            {
                base.OnEnter();

                if (pickupController)
                {
                    GameObject displayParent = pickupController.pickupDisplay.modelObject;
                    _detonationEffectInstance = Instantiate(_detonationEffectPrefab, displayParent.transform);
                    _detonationEffectInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                }
            }

            public override void OnExit()
            {
                Destroy(_detonationEffectInstance);
                _detonationEffectInstance = null;

                base.OnExit();
            }

            public override void FixedUpdate()
            {
                base.FixedUpdate();

                if (pickupController)
                {
                    PickupDef currentPickupDef = PickupCatalog.GetPickupDef(pickupController.pickup.pickupIndex);
                    EquipmentIndex currentEquipmentIndex = currentPickupDef != null ? currentPickupDef.equipmentIndex : EquipmentIndex.None;
                    EquipmentQualityGroupIndex currentEquipmentGroupIndex = QualityCatalog.FindEquipmentQualityGroupIndex(currentEquipmentIndex);
                    QualityTier currentEquipmentQualityTier = QualityCatalog.GetQualityTier(currentEquipmentIndex);

                    if (currentEquipmentGroupIndex != ItemQualitiesContent.EquipmentQualityGroups.QuestVolatileBattery.GroupIndex ||
                        currentEquipmentQualityTier == QualityTier.None)
                    {
                        if (isAuthority)
                        {
                            outer.SetNextState(new Idle());
                        }

                        return;
                    }

                    batteryPickupAttachment._qualityTierContext.QualityTier = currentEquipmentQualityTier;
                }

                if (fixedAge >= duration)
                {
                    if (NetworkServer.active)
                    {
                        ItemQualities.Equipments.QuestVolatileBattery.Detonate(gameObject, damageMultiplier: 10f);

                        if (pickupController)
                        {
                            Destroy(pickupController.gameObject);
                        }
                    }
                }
            }
        }
    }
}
