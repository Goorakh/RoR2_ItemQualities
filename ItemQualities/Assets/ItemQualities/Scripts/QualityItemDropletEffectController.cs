using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Audio;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class QualityItemDropletEffectController : NetworkBehaviour
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

        [SyncVar]
        uint _pickupQualityTierInt;
        public QualityTier PickupQualityTier
        {
            get => (QualityTier)_pickupQualityTierInt - 1;
            private set => _pickupQualityTierInt = (uint)(value + 1);
        }

        void Awake()
        {
            _dropletController = GetComponent<PickupDropletController>();
            if (!_dropletController)
            {
                enabled = false;
                Log.Warning($"{Util.GetGameObjectHierarchyName(gameObject)} is missing PickupDropletController component");
            }
        }

        void Start()
        {
            if (NetworkServer.active)
            {
                QualityTier pickupQualityTier = QualityCatalog.GetQualityTier(_dropletController.pickupState.pickupIndex);
                if (_dropletController.createPickupInfo.pickerOptions != null)
                {
                    foreach (PickupPickerController.Option option in _dropletController.createPickupInfo.pickerOptions)
                    {
                        if (option.available)
                        {
                            pickupQualityTier = QualityCatalog.Max(pickupQualityTier, QualityCatalog.GetQualityTier(option.pickup.pickupIndex));
                        }
                    }
                }

                PickupQualityTier = pickupQualityTier;

                trySpawnQualityEffectServer(PickupQualityTier);
            }
        }

        void OnDestroy()
        {
            QualityTierDef qualityTierDef = QualityCatalog.GetQualityTierDef(PickupQualityTier);
            if (qualityTierDef && qualityTierDef.pickupLandSound)
            {
                PointSoundManager.EmitSoundLocal(qualityTierDef.pickupLandSound.akId, transform.position);
            }
        }

        [Server]
        void trySpawnQualityEffectServer(QualityTier qualityTier)
        {
            QualityTierDef qualityTierDef = QualityCatalog.GetQualityTierDef(qualityTier);
            if (!qualityTierDef)
                return;

            EffectData effectData = new EffectData
            {
                origin = _dropletController.createPickupInfo.position,
            };

            Vector3 velocity = Vector3.zero;
            if (TryGetComponent(out Rigidbody rigidbody))
            {
                velocity = rigidbody.velocity;
            }

            if (velocity.sqrMagnitude > 0f)
            {
                effectData.rotation = Quaternion.FromToRotation(Vector3.up, velocity.normalized);
            }
            else
            {
                effectData.rotation = _dropletController.createPickupInfo.rotation;
            }

            EffectManager.SpawnEffect(qualityTierDef.ChestOpenEffectPrefab, effectData, true);
        }
    }
}
