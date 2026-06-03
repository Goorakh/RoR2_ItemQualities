using HG;
using ItemQualities.Items;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.DirectionalSearch;
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
        Interactor _interactor;
        InteractionDriver _interactionDriver;

        GameObject _currentInteractableObject;
        IInteractable _currentInteractable;

        CharacterModel _cachedCharacterModel;

        MemoizedGetComponentCached<CharacterMasterExtraStatsTracker> _memoizedMasterExtraStatsComponent;

        TemporaryVisualEffect _qualityDeathMarkEffectInstance;
        TemporaryVisualEffect _sprintArmorWeakenEffectInstance;
        TemporaryVisualEffect _constructBubbleEffectInstance;

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

        public float GenesisLoopActivationThreshold { get; private set; } = HealthComponent.lowHealthFraction;

        public CharacterBody LastHitBody { get; private set; }

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

        public float LeechBuffReserveFraction { get; set; } = 0f;

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

        float _gatewayTeleportCooldown;
        Indicator _qualityGatewayPickupTargetIndicator;
        GatewayQualityPickupController _currentGatewayPickupTargetAuthority;
        static readonly GatewayQualityPickupSearch _sharedGatewayPickupTargetSearch = new GatewayQualityPickupSearch
        {
            minDistanceFilter = 2f,
            maxDistanceFilter = 1000f,
            maxAngleFilter = 10f,
            filterByLoS = true,
            sortMode = SortMode.Angle
        };

        public CharacterMasterExtraStatsTracker MasterExtraStatsTracker => _memoizedMasterExtraStatsComponent.Get(_body.masterObject);

        public event Action<DamageInfo> OnIncomingDamageServer;

        public event Action<DamageReport> OnTakeDamageServer;

        public event CharacterMotor.HitGroundDelegate OnHitGroundAuthority;

        public event Action<CharacterMotor.HitGroundInfo> OnHitGroundServer;

        public event Action<DamageReport> OnKilledOther;

        public static event Action<CharacterBodyExtraStatsTracker, GenericSkill> OnSkillActivatedAuthorityGlobal;
        public static event Action<CharacterBodyExtraStatsTracker, GenericSkill> OnSkillActivatedServerGlobal;

        void Awake()
        {
            _netIdentity = GetComponent<NetworkIdentity>();
            _body = GetComponent<CharacterBody>();
            _interactor = GetComponent<Interactor>();
            _interactionDriver = GetComponent<InteractionDriver>();

            ComponentCache.Add(gameObject, this);
        }

        void OnDestroy()
        {
            if (_qualityGatewayPickupTargetIndicator != null)
            {
                _qualityGatewayPickupTargetIndicator.active = false;
            }

            ComponentCache.Remove(gameObject, this);
        }

        void Start()
        {
            if (HasEffectiveAuthority)
            {
                _qualityGatewayPickupTargetIndicator = new Indicator(gameObject, Equipments.Gateway.QualityGatewayPickupTargetIndicatorPrefab);
            }
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

            _body.onSkillActivatedAuthority += onSkillActivatedAuthority;
            _body.onSkillActivatedServer += onSkillActivatedServer;

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

            _body.onSkillActivatedAuthority -= onSkillActivatedAuthority;
            _body.onSkillActivatedServer -= onSkillActivatedServer;

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

                if (_gatewayTeleportCooldown > 0)
                {
                    _gatewayTeleportCooldown -= Time.fixedDeltaTime;
                }

                updateTargets();

                if (_currentGatewayPickupTargetAuthority && Body.inputBank && Body.inputBank.interact.justPressed)
                {
                    _currentGatewayPickupTargetAuthority.OnInteractAuthority(Body);
                    _gatewayTeleportCooldown = 0.3f;
                }
            }

            updateOverlays();
        }

        void updateTargets()
        {
            if (!Body.inputBank)
                return;

            if (_interactionDriver && _currentInteractableObject != _interactionDriver.currentInteractable)
            {
                _currentInteractableObject = _interactionDriver.currentInteractable;
                _currentInteractable = _currentInteractableObject ? _currentInteractableObject.GetComponent<IInteractable>() : null;
            }

            bool hasSelectedInteractable = (_currentInteractable as MonoBehaviour) != null &&
                                           _currentInteractable.GetInteractability(_interactor) > Interactability.Disabled;

            bool isOnInteractCooldown = _interactionDriver && _interactionDriver.interactableCooldown > 0f;

            Ray aimRay = CameraRigController.ModifyAimRayIfApplicable(Body.inputBank.GetAimRay(), gameObject, out _);

            _currentGatewayPickupTargetAuthority = null;
            if (_gatewayTeleportCooldown <= 0f && !(hasSelectedInteractable || isOnInteractCooldown))
            {
                _sharedGatewayPickupTargetSearch.searchOrigin = aimRay.origin;
                _sharedGatewayPickupTargetSearch.searchDirection = aimRay.direction;
                _sharedGatewayPickupTargetSearch.teamIndex = Body.teamComponent.teamIndex;

                _currentGatewayPickupTargetAuthority = _sharedGatewayPickupTargetSearch.SearchCandidatesForSingleTarget(InstanceTracker.GetInstancesList<GatewayQualityPickupController>());
            }

            bool hasGatewayPickupTarget = _currentGatewayPickupTargetAuthority;

            Transform gatewayPickupTargetTransform = null;
            if (hasGatewayPickupTarget)
            {
                if (_currentGatewayPickupTargetAuthority.CoreTransform)
                {
                    gatewayPickupTargetTransform = _currentGatewayPickupTargetAuthority.CoreTransform;
                }
                else
                {
                    gatewayPickupTargetTransform = _currentGatewayPickupTargetAuthority.transform;
                }
            }

            _qualityGatewayPickupTargetIndicator.active = hasGatewayPickupTarget;
            _qualityGatewayPickupTargetIndicator.targetTransform = gatewayPickupTargetTransform;
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

        private void onSkillActivatedAuthority(GenericSkill skill)
        {
            OnSkillActivatedAuthorityGlobal?.Invoke(this, skill);
        }

        private void onSkillActivatedServer(GenericSkill skill)
        {
            OnSkillActivatedServerGlobal?.Invoke(this, skill);
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
            ItemQualityCounts novaOnLowHealth = default;
            if (_body && _body.inventory)
            {
                executeLowHealthElite = _body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ExecuteLowHealthElite);
                phasing = _body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Phasing);
                novaOnLowHealth = _body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.NovaOnLowHealth);
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

            float genesisLoopActivationThreshold;
            switch (novaOnLowHealth.HighestQuality)
            {
                case QualityTier.None:
                    genesisLoopActivationThreshold = HealthComponent.lowHealthFraction;
                    break;
                case QualityTier.Uncommon:
                    genesisLoopActivationThreshold = 0.35f;
                    break;
                case QualityTier.Rare:
                    genesisLoopActivationThreshold = 0.50f;
                    break;
                case QualityTier.Epic:
                    genesisLoopActivationThreshold = 0.75f;
                    break;
                case QualityTier.Legendary:
                    genesisLoopActivationThreshold = 0.90f;
                    break;
                default:
                    Log.Warning($"Quality tier {novaOnLowHealth} is not implemented");
                    genesisLoopActivationThreshold = HealthComponent.lowHealthFraction;
                    break;
            }

            GenesisLoopActivationThreshold = genesisLoopActivationThreshold;
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
            LastHitBody = damageReport.victimBody;
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
            updateTemporaryVisualEffect(ref _sprintArmorWeakenEffectInstance, SprintArmor.BucklerDefenseBigPrefab, _body.bestFitActualRadius, _body.HasBuff(ItemQualitiesContent.Buffs.SprintArmorWeaken));
            updateTemporaryVisualEffect(ref _constructBubbleEffectInstance, ItemQualitiesContent.Prefabs.MinorConstructBubbleEffect, _body.bestFitActualRadius * 1.15f, _body.HasBuff(ItemQualitiesContent.Buffs.ConstructBubble));

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
