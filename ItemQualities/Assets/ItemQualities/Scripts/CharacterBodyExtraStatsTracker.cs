using HG;
using ItemQualities.Items;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class CharacterBodyExtraStatsTracker : NetworkBehaviour, IOnIncomingDamageServerReceiver, IOnTakeDamageServerReceiver
    {
        [SystemInitializer(typeof(BodyCatalog))]
        static void Init()
        {
            foreach (GameObject bodyPrefab in BodyCatalog.allBodyPrefabs)
            {
                bodyPrefab.EnsureComponent<CharacterBodyExtraStatsTracker>();
            }

            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
        }

        static void onCharacterDeathGlobal(DamageReport damageReport)
        {
            if (damageReport.attacker && damageReport.attacker.TryGetComponentCached(out CharacterBodyExtraStatsTracker attackerBodyExtraStats))
            {
                attackerBodyExtraStats.onKilledOther(damageReport);
            }
        }

        NetworkIdentity _netIdentity;

        CharacterBody _body;

        CharacterModel _cachedCharacterModel;

        TemporaryVisualEffect _qualityDeathMarkEffectInstance;
        TemporaryVisualEffect _sprintArmorStrongEffectInstance;

        TemporaryOverlayInstance _healCritBoostOverlay;

        public ItemQualityCounts LastExtraStatsOnLevelUpCounts = default;

        public float ExecuteBossHealthFraction { get; private set; }

        public float StealthKitActivationThreshold { get; private set; } = HealthComponent.lowHealthFraction;

        public float AirControlBonus { get; private set; } = 1f;

        public bool HasEffectiveAuthority => Util.HasEffectiveAuthority(_netIdentity);

        [SyncVar(hook = nameof(hookSetIsPerformingQuailJump))]
        bool _isPerformingQuailJump;
        public bool IsPerformingQuailJump
        {
            get => _isPerformingQuailJump;
            private set
            {
                _isPerformingQuailJump = value;

                if (HasEffectiveAuthority && !NetworkServer.active)
                {
                    CmdSetPerformingQuailJump(_isPerformingQuailJump);
                }
            }
        }

        public Run.FixedTimeStamp LastQuailLandTimeAuthority { get; private set; } = Run.FixedTimeStamp.positiveInfinity;

        public Vector3 LastQuailJumpVelocityAuthority { get; private set; } = Vector3.zero;

        public int QuailJumpComboAuthority { get; private set; }

        public bool HasHadAnyQualityDeathMarkDebuffServer { get; private set; }

        public float CurrentMedkitProcTimeSinceLastHit { get; set; } = 0f;

        public int EliteKillCount { get; private set; } = 0;

        public CharacterMasterExtraStatsTracker MasterExtraStatsTracker { get; private set; }

        public event Action<DamageInfo> OnIncomingDamageServer;

        public event Action<DamageReport> OnTakeDamageServer;

        public event CharacterMotor.HitGroundDelegate OnHitGroundAuthority;

        public event Action<CharacterMotor.HitGroundInfo> OnHitGroundServer;

        public event Action<DamageReport> OnKilledOther;

        void Awake()
        {
            _netIdentity = GetComponent<NetworkIdentity>();
            _body = GetComponent<CharacterBody>();

            ComponentCache.Add(gameObject, this);
        }

        void Start()
        {
            if (_body.master)
            {
                MasterExtraStatsTracker = _body.master.GetComponentCached<CharacterMasterExtraStatsTracker>();
            }
        }

        void OnDestroy()
        {
            ComponentCache.Remove(gameObject, this);
        }

        void OnEnable()
        {
            _body.onRecalculateStats += onBodyRecalculateStats;

            if (_body.characterMotor)
            {
                _body.characterMotor.onHitGroundAuthority += onHitGroundAuthority;
            }

            if (_body.modelLocator)
            {
                _body.modelLocator.onModelChanged += refreshModelReference;
            }

            refreshModelReference(_body.modelLocator ? _body.modelLocator.modelTransform : null);

            recalculateExtraStats();
        }

        void OnDisable()
        {
            _body.onRecalculateStats -= onBodyRecalculateStats;

            if (_body.characterMotor)
            {
                _body.characterMotor.onHitGroundAuthority -= onHitGroundAuthority;
            }

            if (_body.modelLocator)
            {
                _body.modelLocator.onModelChanged -= refreshModelReference;
            }
        }

        void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                if (!HasHadAnyQualityDeathMarkDebuffServer && DeathMark.HasAnyQualityDeathMarkDebuff(_body))
                {
                    HasHadAnyQualityDeathMarkDebuffServer = true;
                }
            }

            if (HasEffectiveAuthority)
            {
                if (QuailJumpComboAuthority > 0 && !IsPerformingQuailJump && LastQuailLandTimeAuthority.timeSince > 0.1f)
                {
                    QuailJumpComboAuthority = 0;
                }
            }

            updateOverlays();
        }

        void refreshModelReference(Transform modelTransform)
        {
            _cachedCharacterModel = modelTransform ? modelTransform.GetComponent<CharacterModel>() : null;
        }

        void updateOverlays()
        {
            void setOverlay(ref TemporaryOverlayInstance overlayInstance, Material material, bool active)
            {
                if (!material)
                    return;

                if (!_cachedCharacterModel)
                {
                    active = false;
                }

                bool overlayActive = overlayInstance != null && overlayInstance.assignedCharacterModel == _cachedCharacterModel;
                if (overlayActive == active)
                    return;

                if (overlayInstance != null)
                {
                    overlayInstance.RemoveFromCharacterModel();
                    overlayInstance = null;
                }

                if (active)
                {
                    overlayInstance = new TemporaryOverlayInstance(gameObject)
                    {
                        duration = float.PositiveInfinity,
                        destroyComponentOnEnd = true,
                        originalMaterial = material
                    };

                    overlayInstance.AddToCharacterModel(_cachedCharacterModel);
                }
            }

            setOverlay(ref _healCritBoostOverlay, ItemQualitiesContent.Materials.HealCritBoost, _body.HasBuff(ItemQualitiesContent.Buffs.HealCritBoost));
        }

        void onBodyRecalculateStats(CharacterBody body)
        {
            recalculateExtraStats();
        }

        void recalculateExtraStats()
        {
            ItemQualityCounts executeLowHealthElite = default;
            ItemQualityCounts phasing = default;
            ItemQualityCounts jumpBoost = default;
            if (_body && _body.inventory)
            {
                executeLowHealthElite = _body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ExecuteLowHealthElite);
                phasing = _body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Phasing);
                jumpBoost = _body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.JumpBoost);
            }

            ExecuteBossHealthFraction = Util.ConvertAmplificationPercentageIntoReductionNormalized(amplificationNormal:
                (0.10f * executeLowHealthElite.UncommonCount) +
                (0.15f * executeLowHealthElite.RareCount) +
                (0.25f * executeLowHealthElite.EpicCount) +
                (0.40f * executeLowHealthElite.LegendaryCount));

            float stealthKitActivationThresholdIncrease = 1f;
            stealthKitActivationThresholdIncrease *= Mathf.Pow(1f - 0.10f, phasing.UncommonCount);
            stealthKitActivationThresholdIncrease *= Mathf.Pow(1f - 0.25f, phasing.RareCount);
            stealthKitActivationThresholdIncrease *= Mathf.Pow(1f - 0.50f, phasing.EpicCount);
            stealthKitActivationThresholdIncrease *= Mathf.Pow(1f - 0.75f, phasing.LegendaryCount);

            StealthKitActivationThreshold = 1f - ((1f - HealthComponent.lowHealthFraction) * stealthKitActivationThresholdIncrease);

            float airControlBonus = 0f;

            if (IsPerformingQuailJump)
            {
                switch (jumpBoost.HighestQuality)
                {
                    case QualityTier.Uncommon:
                        airControlBonus += 0.10f;
                        break;
                    case QualityTier.Rare:
                        airControlBonus += 0.20f;
                        break;
                    case QualityTier.Epic:
                        airControlBonus += 0.30f;
                        break;
                    case QualityTier.Legendary:
                        airControlBonus += 0.50f;
                        break;
                }
            }

            AirControlBonus = airControlBonus;
        }

        void IOnIncomingDamageServerReceiver.OnIncomingDamageServer(DamageInfo damageInfo)
        {
            OnIncomingDamageServer?.Invoke(damageInfo);
        }

        void IOnTakeDamageServerReceiver.OnTakeDamageServer(DamageReport damageReport)
        {
            OnTakeDamageServer?.Invoke(damageReport);
        }

        void onKilledOther(DamageReport damageReport)
        {
            if (damageReport.victimIsElite)
            {
                EliteKillCount++;
            }

            OnKilledOther?.Invoke(damageReport);
        }

        void onHitGroundAuthority(ref CharacterMotor.HitGroundInfo hitGroundInfo)
        {
            if (IsPerformingQuailJump)
            {
                LastQuailLandTimeAuthority = Run.FixedTimeStamp.now;
                IsPerformingQuailJump = false;
            }

            OnHitGroundAuthority?.Invoke(ref hitGroundInfo);

            CmdOnHitGround(hitGroundInfo.velocity, hitGroundInfo.position, hitGroundInfo.isValidForEffect);
        }

        [Command]
        void CmdOnHitGround(Vector3 velocity, Vector3 position, bool isValidForEffect)
        {
            OnHitGroundServer?.Invoke(new CharacterMotor.HitGroundInfo
            {
                velocity = velocity,
                position = position,
                isValidForEffect = isValidForEffect,
                ownerBodyObject = gameObject
            });
        }

        public void UpdateAllTemporaryVisualEffects()
        {
            updateTemporaryVisualEffect(ref _qualityDeathMarkEffectInstance, ItemQualitiesContent.Prefabs.DeathMarkQualityEffect, _body.radius, DeathMark.HasAnyQualityDeathMarkDebuff(_body));
            updateTemporaryVisualEffect(ref _sprintArmorStrongEffectInstance, SprintArmor.BucklerDefenseBigPrefab, _body.radius * 1.5f, _body.HasBuff(ItemQualitiesContent.Buffs.SprintArmorStrong));

            void updateTemporaryVisualEffect(ref TemporaryVisualEffect temporaryEffect, GameObject effectPrefab, float effectRadius, bool active)
            {
                _body.UpdateSingleTemporaryVisualEffect(ref temporaryEffect, effectPrefab, effectRadius, active);

                // Fix temp effects not spawning if disabled and re-enabled within the exit duration
                if (!active && temporaryEffect && temporaryEffect.visualState == TemporaryVisualEffect.VisualState.Exit)
                {
                    temporaryEffect = null;
                }
            }
        }

        public void OnQuailJumpAuthority()
        {
            if (!HasEffectiveAuthority)
            {
                Log.Warning("Caller must have authority");
                return;
            }

            Vector3 jumpVelocity = _body.characterMotor ? _body.characterMotor.velocity : Vector3.zero;

            if (QuailJumpComboAuthority > 0)
            {
                Vector3 horizontalJumpVelocity = jumpVelocity;
                horizontalJumpVelocity.y = 0f;

                Vector3 lastJumpHorizontalVelocity = LastQuailJumpVelocityAuthority;
                lastJumpHorizontalVelocity.y = 0f;

                bool resetJumpCombo = false;
                if (horizontalJumpVelocity.sqrMagnitude > 0)
                {
                    if (lastJumpHorizontalVelocity.sqrMagnitude > 0)
                    {
                        float jumpAngleDiff = Vector3.Angle(horizontalJumpVelocity.normalized, lastJumpHorizontalVelocity.normalized);
                        if (jumpAngleDiff >= 60f)
                        {
                            resetJumpCombo = true;
                        }
                    }
                }
                else if (lastJumpHorizontalVelocity.sqrMagnitude > 0)
                {
                    resetJumpCombo = true;
                }

                if (resetJumpCombo)
                {
                    QuailJumpComboAuthority = 0;
                }
            }

            IsPerformingQuailJump = true;
            LastQuailJumpVelocityAuthority = jumpVelocity;
            QuailJumpComboAuthority++;
        }

        [Command]
        void CmdSetPerformingQuailJump(bool performing)
        {
            IsPerformingQuailJump = performing;
        }

        void hookSetIsPerformingQuailJump(bool performingQuailJump)
        {
            bool changed = _isPerformingQuailJump != performingQuailJump;
            _isPerformingQuailJump = performingQuailJump;

            if (changed)
            {
                _body.MarkAllStatsDirty();
            }
        }
    }
}
