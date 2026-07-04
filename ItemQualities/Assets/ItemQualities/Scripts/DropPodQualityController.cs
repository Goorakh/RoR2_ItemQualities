using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Audio;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public class DropPodQualityController : MonoBehaviour
    {
        bool appliedQuality;
        bool hidQuality;

        GenericPickupController _pickupController;
        Transform _qualityPickupDisplay;

        [SystemInitializer]
        static void Init()
        {
            On.EntityStates.SurvivorPod.BatteryPanel.Opening.OnEnter += Opening_OnEnter;
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_SurvivorPod.SurvivorPod_prefab).OnSuccess(SurvivorPod =>
            {
                SurvivorPod.AddComponent<DropPodQualityController>();
                SurvivorPod.AddComponent<QualityTierContext>();
            });
        }

        public void FixedUpdate()
        {
            if (!getWorldPickupAndController())
                return;
            if (NetworkServer.active && !appliedQuality)
            {
                appliedQuality = true;
                Xoroshiro128Plus rng = new Xoroshiro128Plus(Run.instance.treasureRng.nextUlong);
                _pickupController.pickup = _pickupController.pickup.WithPickupIndex(DropTableQualityHandler.RollQuality(_pickupController.pickup.pickupIndex, rng, new PickupRollInfo(null, TeamIndex.Player)));
            }

            if (!hidQuality)
            {
                if (_qualityPickupDisplay)
                {
                    _qualityPickupDisplay.gameObject.SetActive(false);
                    hidQuality = true;
                }
            }
        }

        bool getWorldPickupAndController()
        {
            if (_pickupController && _qualityPickupDisplay)
                return true;
            ModelLocator modelLocator = GetComponent<ModelLocator>();
            if (!modelLocator)
                return false;
            Transform batteryAttachmentPoint = modelLocator.modelChildLocator.FindChild("BatteryAttachmentPoint");
            if (!batteryAttachmentPoint)
                return false;
            Transform worldPickup = batteryAttachmentPoint.Find("QuestVolatileBatteryWorldPickup(Clone)");
            if (!worldPickup)
                return false;
            _pickupController = worldPickup.GetComponent<GenericPickupController>();
            if (!_pickupController)
                return false;
            _qualityPickupDisplay = worldPickup.Find("PickupDisplay/QualityPickupDisplay(Clone)");
            if (_qualityPickupDisplay)
            {
                return true;
            }

            return false;
        }

        private static void Opening_OnEnter(On.EntityStates.SurvivorPod.BatteryPanel.Opening.orig_OnEnter orig, EntityStates.SurvivorPod.BatteryPanel.Opening self)
        {
            orig(self);
            if (!self.podInfo.podAnimator)
                return;
            ChildLocator childLocator = self.podInfo.podAnimator.GetComponent<ChildLocator>();
            if (!childLocator)
                return;
            Transform batteryAttachmentPoint = childLocator.FindChild("BatteryAttachmentPoint");
            if (!batteryAttachmentPoint)
                return;
            Transform worldPickup = batteryAttachmentPoint.Find("QuestVolatileBatteryWorldPickup(Clone)");
            if (!worldPickup)
                return;
            GenericPickupController pickupController = worldPickup.GetComponent<GenericPickupController>();
            if (!pickupController || pickupController.pickup == null)
                return;

            Transform qualityPickupDisplay = worldPickup.Find("PickupDisplay/QualityPickupDisplay(Clone)");
            if (qualityPickupDisplay)
            {
                qualityPickupDisplay.gameObject.SetActive(true);
            }

            EquipmentIndex equipmentIndex = PickupCatalog.GetPickupDef(pickupController.pickup.pickupIndex)?.equipmentIndex ?? EquipmentIndex.None;
            if (QualityCatalog.GetQualityTier(equipmentIndex) > QualityTier.None)
            {
                EffectData effectData = new EffectData
                {
                    origin = worldPickup.position,
                    rotation = worldPickup.rotation
                };

                QualityTierDef qualityTierDef = QualityCatalog.GetQualityTierDef(QualityCatalog.GetQualityTier(pickupController.pickup.pickupIndex));
                EffectManager.SpawnEffect(qualityTierDef.ChestOpenEffectPrefab, effectData, false);
                PointSoundManager.EmitSoundLocal(qualityTierDef.pickupLandSound.akId, worldPickup.position);
            }
        }
    }
}
