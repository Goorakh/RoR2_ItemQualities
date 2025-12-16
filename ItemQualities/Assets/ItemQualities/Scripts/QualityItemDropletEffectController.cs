using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPaths.Version_1_35_0;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class QualityItemDropletEffectController : MonoBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_Common.PickupDroplet_prefab).OnSuccess(pickupDropletPrefab =>
            {
                pickupDropletPrefab.EnsureComponent<QualityItemDropletEffectController>();
            });
        }

        PickupDropletController _dropletController;

        void Awake()
        {
            _dropletController = GetComponent<PickupDropletController>();
        }

        void Start()
        {
            if (NetworkServer.active)
            {
                trySpawnQualityEffectServer();
            }
        }

        void trySpawnQualityEffectServer()
        {
            if (!NetworkServer.active)
                return;

            if (!_dropletController || !_dropletController.pickupState.isValid || !_dropletController.createPickupInfo.chest)
                return;

            ChestBehavior chest = _dropletController.createPickupInfo.chest;

            QualityTier qualityTier = QualityCatalog.GetQualityTier(_dropletController.pickupState.pickupIndex);
            if (qualityTier == QualityTier.None)
                return;

            QualityTierDef qualityTierDef = QualityCatalog.GetQualityTierDef(qualityTier);

            Transform effectSpawnTransform = null;
            int effectSpawnTransformChildIndex = -1;
            if (chest.TryGetComponent(out ModelLocator modelLocator))
            {
                ChildLocator chestModelChildLocator = modelLocator.modelChildLocator;
                if (chestModelChildLocator)
                {
                    effectSpawnTransformChildIndex = chestModelChildLocator.FindChildIndex("BurstCenter");
                    effectSpawnTransform = chestModelChildLocator.FindChild(effectSpawnTransformChildIndex);
                }
            }

            EffectData effectData = new EffectData
            {
                origin = effectSpawnTransform ? effectSpawnTransform.position : _dropletController.createPickupInfo.position,
                rotation = Quaternion.identity,
            };

            if (effectSpawnTransformChildIndex != -1)
            {
                effectData.SetChildLocatorTransformReference(chest.gameObject, effectSpawnTransformChildIndex);
            }

            EffectManager.SpawnEffect(qualityTierDef.ChestOpenEffectPrefab, effectData, true);
        }
    }
}
