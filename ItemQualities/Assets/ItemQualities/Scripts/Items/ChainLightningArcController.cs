using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.Orbs;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    [RequireComponent(typeof(NetworkedBodyAttachment))]
    public sealed class ChainLightningArcController : NetworkBehaviour
    {
        public static readonly float FireInterval = 0.2f;

        [SyncVar]
        [NonSerialized]
        public GameObject Attacker;

        [SyncVar]
        [NonSerialized]
        public bool IsCrit;

        [SyncVar]
        [NonSerialized]
        public float Duration;

        private NetworkedBodyAttachment _bodyAttachment;

        private MemoizedGetComponent<CharacterBody> _attackerBody;

        private float _stopwatch;

        private float _fireTimer;

        private void Awake()
        {
            _bodyAttachment = GetComponent<NetworkedBodyAttachment>();
        }

        private void OnEnable()
        {
            InstanceTracker.Add(this);
        }

        private void OnDisable()
        {
            InstanceTracker.Remove(this);
        }

        private void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                fixedUpdateServer(Time.fixedDeltaTime);
            }
        }

        private void fixedUpdateServer(float deltaTime)
        {
            _stopwatch += deltaTime;
            if (_stopwatch >= Duration)
            {
                Destroy(gameObject);
                return;
            }

            _fireTimer += deltaTime;
            if (_fireTimer >= FireInterval)
            {
                _fireTimer -= FireInterval;
                fireArc();
            }
        }

        private void fireArc()
        {
            CharacterBody attackerBody = _attackerBody.Get(Attacker);
            if (!attackerBody || !attackerBody.inventory)
                return;

            ItemQualityCounts attackerChainLightning = attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ChainLightning);

            float damageCoefficient = 1.5f * attackerChainLightning.TotalQualityCount;
            float maxRange = 20f + (2f * attackerChainLightning.TotalCount);

            Vector3 firePosition = transform.position;
            if (_bodyAttachment.attachedBody)
            {
                firePosition = _bodyAttachment.attachedBody.corePosition;
            }

            LightningOrb lightningOrb = new LightningOrb();
            lightningOrb.origin = firePosition;
            lightningOrb.damageValue = attackerBody.damage * damageCoefficient;
            lightningOrb.isCrit = IsCrit;
            lightningOrb.bouncesRemaining = 0;
            lightningOrb.bouncedObjects = new List<HealthComponent>();
            lightningOrb.teamIndex = attackerBody.teamComponent.teamIndex;
            lightningOrb.attacker = attackerBody.gameObject;
            lightningOrb.procCoefficient = 0f;
            lightningOrb.lightningType = LightningOrb.LightningType.Ukulele;
            lightningOrb.damageColorIndex = DamageColorIndex.Item;
            lightningOrb.range = maxRange;

            if (_bodyAttachment.attachedBody)
            {
                lightningOrb.bouncedObjects.Add(_bodyAttachment.attachedBody.healthComponent);
            }

            HurtBox target = lightningOrb.PickNextTarget(lightningOrb.origin);
            if (target)
            {
                lightningOrb.target = target;
                OrbManager.instance.AddOrb(lightningOrb);
            }
        }

        public static void AddToBody(GameObject bodyObject, GameObject attacker, bool isCrit, float duration)
        {
            if (!NetworkServer.active)
            {
                Log.Warning("Called on client");
                return;
            }

            if (!bodyObject)
                return;

            ChainLightningArcController lightningArcController = null;
            foreach (ChainLightningArcController chainLightningArcController in InstanceTracker.GetInstancesList<ChainLightningArcController>())
            {
                if (ReferenceEquals(chainLightningArcController._bodyAttachment.attachedBodyObject, bodyObject) &&
                    ReferenceEquals(chainLightningArcController.Attacker, attacker))
                {
                    lightningArcController = chainLightningArcController;
                    break;
                }
            }

            if (!lightningArcController)
            {
                GameObject lightningArcControllerObj = Instantiate(ItemQualitiesContent.NetworkedPrefabs.ChainLightningArcAttachment);
                lightningArcController = lightningArcControllerObj.GetComponent<ChainLightningArcController>();
                lightningArcController.Attacker = attacker;

                NetworkedBodyAttachment lightningArcControllerAttachment = lightningArcControllerObj.GetComponent<NetworkedBodyAttachment>();
                lightningArcControllerAttachment.AttachToGameObjectAndSpawn(bodyObject);
            }

            lightningArcController.Duration += duration;
            lightningArcController.IsCrit |= isCrit;
        }
    }
}
