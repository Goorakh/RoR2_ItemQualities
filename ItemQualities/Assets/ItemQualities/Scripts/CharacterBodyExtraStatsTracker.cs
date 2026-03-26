using HG;
using ItemQualities.Items;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using System.Runtime.CompilerServices;
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
            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        private static void onServerDamageDealt(DamageReport damageReport)
        {
            if (damageReport.attacker && damageReport.attacker.TryGetComponentCached(out CharacterBodyExtraStatsTracker attackerBodyExtraStats))
            {
                attackerBodyExtraStats.onDamagedOther(damageReport);
            }
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

        MemoizedGetComponentCached<CharacterMasterExtraStatsTracker> _memoizedMasterExtraStatsComponent;

        TemporaryVisualEffect _qualityDeathMarkEffectInstance;
        TemporaryVisualEffect _sprintArmorStrongEffectInstance;

        TemporaryOverlayInstance _healCritBoostOverlay;

        int _weakPointsEnabledCounterServer;

        [SyncVar]
        byte _weakPointHurtBoxIndexPlusOne;
        public int WeakPointHurtBoxIndex
        {
            get => _weakPointHurtBoxIndexPlusOne - 1;
            private set => _weakPointHurtBoxIndexPlusOne = (byte)(value + 1);
        }

        public ItemQualityCounts LastExtraStatsOnLevelUpCounts = default;

        public CharacterBody Body => _body;

        public float ExecuteBossHealthFraction { get; private set; }

        public float StealthKitActivationThreshold { get; private set; } = HealthComponent.lowHealthFraction;

        public CharacterBody lastDamaged;

        public bool HasEffectiveAuthority => Util.HasEffectiveAuthority(_netIdentity);

        [SyncVar]
        public int ParryStoredProjectileIndex = -1;

        public float ParryStoredProjectileDamage;

        public bool ParryStoredProjectileCrit;

        [SyncVar]
        int _parryStoredProjectileAttackerBodyIndexInt;
        public BodyIndex ParryStoredProjectileAttackerBodyIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (BodyIndex)(_parryStoredProjectileAttackerBodyIndexInt - 1);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _parryStoredProjectileAttackerBodyIndexInt = (int)value + 1;
        }

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

        public float WeakPointCritMultiplierBonusServer { get; set; }

        public int WeakPointsEnabledCounterServer
        {
            get
            {
                return _weakPointsEnabledCounterServer;
            }
            [Server]
            set
            {
                bool weakPointsWasEnabled = _weakPointsEnabledCounterServer > 0;
                bool weakPointsIsEnabled = value > 0;

                _weakPointsEnabledCounterServer = value;

                if (weakPointsWasEnabled != weakPointsIsEnabled)
                {
                    if (weakPointsIsEnabled && _body.hurtBoxGroup && _body.hurtBoxGroup.hurtBoxes.Length > 0)
                    {
                        WeakPointHurtBoxIndex = UnityEngine.Random.Range(0, _body.hurtBoxGroup.hurtBoxes.Length);
                    }
                    else
                    {
                        WeakPointHurtBoxIndex = -1;
                    }
                }
            }
        }

        public CharacterMasterExtraStatsTracker MasterExtraStatsTracker => _memoizedMasterExtraStatsComponent.Get(_body.masterObject);

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

        void OnDestroy()
        {
            ComponentCache.Remove(gameObject, this);
        }

        void OnEnable()
        {
            InstanceTracker.Add(this);

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

            refreshModelReference(null);

            InstanceTracker.Remove(this);
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
                if (QuailJumpComboAuthority > 0 && !IsPerformingQuailJump && LastQuailLandTimeAuthority.timeSince > 0.15f)
                {
                    QuailJumpComboAuthority = 0;
                }
            }

            updateOverlays();
        }

        void refreshModelReference(Transform modelTransform)
        {
            GameObject cachedModelObject = _cachedCharacterModel ? _cachedCharacterModel.gameObject : null;
            GameObject newModelObject = modelTransform ? modelTransform.gameObject : null;
            if (cachedModelObject == newModelObject)
                return;

            _cachedCharacterModel = modelTransform ? modelTransform.GetComponent<CharacterModel>() : null;

            if (NetworkServer.active)
            {
                if (_weakPointsEnabledCounterServer > 0 &&
                    modelTransform &&
                    modelTransform.TryGetComponent(out HurtBoxGroup hurtBoxGroup) &&
                    hurtBoxGroup.hurtBoxes.Length > 0)
                {
                    WeakPointHurtBoxIndex = UnityEngine.Random.Range(0, hurtBoxGroup.hurtBoxes.Length);
                }
                else
                {
                    WeakPointHurtBoxIndex = -1;
                }
            }
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
            if (_body && _body.inventory)
            {
                executeLowHealthElite = _body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ExecuteLowHealthElite);
                phasing = _body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Phasing);
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
        }

        void IOnIncomingDamageServerReceiver.OnIncomingDamageServer(DamageInfo damageInfo)
        {
            BuffQualityCounts bugBlock = Body.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.BugBlock);
            if (bugBlock.TotalQualityCount > 0 && damageInfo.damage > 0 && !damageInfo.rejected)
            {
                bool evade = false;
                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    // Uncommon: 10%
                    // Rare: 20%
                    // Epic: 30%
                    // Legendary: 40%
                    float evadeChance = ((int)qualityTier + 1) * 10;

                    ref int buffCount = ref bugBlock[qualityTier];
                    if (buffCount > 0 && RollUtil.CheckRoll(evadeChance, _body.master, false))
                    {
                        evade = true;

                        buffCount--;
                        Body.RemoveBuff(ItemQualitiesContent.BuffQualityGroups.BugBlock.GetBuffIndex(qualityTier));
                        break;
                    }
                }

                if (evade)
                {
                    EffectData effectData = new EffectData
                    {
                        origin = damageInfo.position
                    };

                    EffectManager.SpawnEffect(ItemQualitiesContent.Prefabs.BugBlockProcEffect, effectData, true);

                    damageInfo.rejected = true;
                }
            }

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

        void onDamagedOther(DamageReport damageReport)
        {
            lastDamaged = damageReport.victimBody;
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

            IsPerformingQuailJump = true;
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
