using HG;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Audio;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class TimedChestQualityController : MonoBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.TimedChestController.OnEnable += TimedChestController_OnEnable;

            On.EntityStates.TimedChest.Opening.OnEnter += Opening_OnEnter;
            IL.EntityStates.TimedChest.Opening.FixedUpdate += Opening_FixedUpdate;
        }

        static void TimedChestController_OnEnable(On.RoR2.TimedChestController.orig_OnEnable orig, TimedChestController self)
        {
            orig(self);
            self.EnsureComponent<TimedChestQualityController>();
        }

        static void Opening_OnEnter(On.EntityStates.TimedChest.Opening.orig_OnEnter orig, EntityStates.TimedChest.Opening self)
        {
            orig(self);

            if (self.TryGetComponent(out TimedChestQualityController timedChestQualityController))
            {
                timedChestQualityController.onOpening();
            }
        }

        static void Opening_FixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchStfld<EntityStates.TimedChest.Opening>(nameof(EntityStates.TimedChest.Opening.hasGrantedAchievement))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<EntityStates.TimedChest.Opening>>(onOpened);

            static void onOpened(EntityStates.TimedChest.Opening self)
            {
                if (self.TryGetComponent(out TimedChestQualityController timedChestQualityController))
                {
                    timedChestQualityController.onOpened();
                }
            }
        }

        Xoroshiro128Plus _rng;

        ModelLocator _modelLocator;

        GenericPickupController _pickupController;
        QualityPickupDisplayController _qualityPickupDisplayController;

        bool _foundPickupController;

        float _findPickupTimer;

        void Awake()
        {
            _modelLocator = GetComponent<ModelLocator>();

            if (NetworkServer.active)
            {
                _rng = new Xoroshiro128Plus(Run.instance.treasureRng.nextUlong);
            }
        }

        void FixedUpdate()
        {
            if (!_foundPickupController)
            {
                _findPickupTimer += Time.fixedDeltaTime;
                if (_findPickupTimer >= 0.3f)
                {
                    _findPickupTimer = 0f;

                    if (_modelLocator && _modelLocator.modelTransform)
                    {
                        GenericPickupController pickupController = _modelLocator.modelTransform.GetComponentInChildren<GenericPickupController>();
                        if (pickupController)
                        {
                            _pickupController = pickupController;

                            _qualityPickupDisplayController = pickupController.pickupDisplay ? pickupController.pickupDisplay.GetComponentInChildren<QualityPickupDisplayController>() : null;
                            if (_qualityPickupDisplayController)
                            {
                                _qualityPickupDisplayController.enabled = false;
                            }

                            _foundPickupController = true;

                            if (NetworkServer.active)
                            {
                                rollQualityServer();
                            }
                        }
                    }
                }
            }
        }

        void rollQualityServer()
        {
            _pickupController.pickup = _pickupController.pickup.WithPickupIndex(DropTableQualityHandler.RollQuality(_pickupController.pickup.pickupIndex, _rng, new PickupRollInfo(null, TeamIndex.Player)));
            Log.Debug($"{Util.GetGameObjectHierarchyName(gameObject)}: Rolled quality for pickup ({QualityCatalog.GetQualityTier(_pickupController.pickup.pickupIndex)})");
        }

        void onOpening()
        {
            if (_qualityPickupDisplayController)
            {
                _qualityPickupDisplayController.enabled = true;
            }

            if (_pickupController)
            {
                QualityTier pickupQualityTier = QualityCatalog.GetQualityTier(_pickupController.pickup.pickupIndex);
                if (pickupQualityTier > QualityTier.None)
                {
                    QualityTierDef qualityTierDef = QualityCatalog.GetQualityTierDef(pickupQualityTier);

                    int effectTransformIndex = -1; 
                    Transform effectTransform = null; 

                    ChildLocator modelChildLocator = _modelLocator ? _modelLocator.modelChildLocator : null;
                    if (modelChildLocator)
                    {
                        effectTransformIndex = modelChildLocator.FindChildIndex("BurstCenter");
                        effectTransform = modelChildLocator.FindChild(effectTransformIndex);
                    }

                    EffectData effectData = new EffectData
                    {
                        origin = effectTransform ? effectTransform.position : _pickupController.transform.position,
                        rotation = effectTransform ? effectTransform.rotation : _pickupController.transform.rotation
                    };

                    if (effectTransformIndex != -1)
                    {
                        effectData.SetChildLocatorTransformReference(gameObject, effectTransformIndex);
                    }

                    EffectManager.SpawnEffect(qualityTierDef.ChestOpenEffectPrefab, effectData, false);
                }
            }
        }

        void onOpened()
        {
            if (_pickupController)
            {
                QualityTier pickupQualityTier = QualityCatalog.GetQualityTier(_pickupController.pickup.pickupIndex);
                if (pickupQualityTier > QualityTier.None)
                {
                    QualityTierDef qualityTierDef = QualityCatalog.GetQualityTierDef(pickupQualityTier);
                    if (!string.IsNullOrWhiteSpace(qualityTierDef.pickupLandSoundEventName))
                    {
                        PointSoundManager.EmitSoundLocal(qualityTierDef.pickupLandSoundEventName, transform.position);
                    }
                }
            }
        }
    }
}
