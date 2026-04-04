using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Audio;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public class DropPodQualityController : MonoBehaviour
    {
        static Xoroshiro128Plus _rng;
        bool appliedQuality;
        bool hidQuality;

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
            if (appliedQuality && hidQuality)
                return;
            ChildLocator childLocator = transform.Find("Base/mdlEscapePod").GetComponent<ChildLocator>();
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

            if (NetworkServer.active)
            {
                if (!appliedQuality)
                {
                    appliedQuality = true;
                    _rng = new Xoroshiro128Plus(Run.instance.treasureRng.nextUlong);
                    pickupController.pickup = pickupController.pickup.WithPickupIndex(DropTableQualityHandler.RollQuality(pickupController.pickup.pickupIndex, _rng, new PickupRollInfo(null, TeamIndex.Player)));
                }
            } else {
                appliedQuality = true;
            }

            if (QualityCatalog.GetQualityTier(pickupController.pickup.pickupIndex) > QualityTier.None)
            {
                Transform qualityPickupDisplay = worldPickup.Find("PickupDisplay/QualityPickupDisplay(Clone)");
                if (qualityPickupDisplay)
                {
                    qualityPickupDisplay.gameObject.SetActive(false);
                    hidQuality = true;
                }
            }
            else
            {
                hidQuality = true;
            }

            
        }

        private static void Opening_OnEnter(On.EntityStates.SurvivorPod.BatteryPanel.Opening.orig_OnEnter orig, EntityStates.SurvivorPod.BatteryPanel.Opening self)
        {
            orig(self);
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

            EffectData effectData = new EffectData
            {
                origin = worldPickup.position,
                rotation = worldPickup.rotation
            };

            Transform qualityPickupDisplay = worldPickup.Find("PickupDisplay/QualityPickupDisplay(Clone)");
            if (qualityPickupDisplay)
            {
                qualityPickupDisplay.gameObject.SetActive(true);
            }

            QualityTierDef qualityTierDef = QualityCatalog.GetQualityTierDef(QualityCatalog.GetQualityTier(pickupController.pickup.pickupIndex));
            EffectManager.SpawnEffect(qualityTierDef.ChestOpenEffectPrefab, effectData, false);
            PointSoundManager.EmitSoundLocal(qualityTierDef.pickupLandSoundEventName, worldPickup.position);
        }
    }
}
