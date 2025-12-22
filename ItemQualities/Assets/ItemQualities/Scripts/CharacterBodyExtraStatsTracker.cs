using HG;
using ItemQualities.Items;
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
            if (damageReport.attacker && damageReport.attacker.TryGetComponent(out CharacterBodyExtraStatsTracker attackerBodyExtraStats))
            {
                attackerBodyExtraStats.onKilledOther(damageReport);
            }
        }

        NetworkIdentity _netIdentity;

        CharacterBody _body;

        CharacterModel _cachedCharacterModel;

        bool _statsDirty;

        TemporaryVisualEffect _qualityDeathMarkEffectInstance;
        TemporaryVisualEffect _sprintArmorStrongEffectInstance;

        TemporaryOverlayInstance _healCritBoostOverlay;

        public ItemQualityCounts LastExtraStatsOnLevelUpCounts = default;

        const float BaseCrowbarMinHealthFraction = 0.9f;
        float _crowbarMinHealthFraction = BaseCrowbarMinHealthFraction;
        public float CrowbarMinHealthFraction
        {
            get
            {
                recalculateStatsIfNeeded();
                return _crowbarMinHealthFraction;
            }
        }

        float _executeBossHealthFraction;
        public float ExecuteBossHealthFraction
        {
            get
            {
                recalculateStatsIfNeeded();
                return _executeBossHealthFraction;
            }
        }

        float _stealthKitActivationThreshold = HealthComponent.lowHealthFraction;
        public float StealthKitActivationThreshold
        {
            get
            {
                recalculateStatsIfNeeded();
                return _stealthKitActivationThreshold;
            }
        }

        float _airControlBonus = 1f;
        public float AirControlBonus
        {
            get
            {
                recalculateStatsIfNeeded();
                return _airControlBonus;
            }
        }

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
        }

        void Start()
        {
            if (_body.master)
            {
                MasterExtraStatsTracker = _body.master.GetComponent<CharacterMasterExtraStatsTracker>();
            }
        }

        void OnEnable()
        {
            recalculateExtraStats();
            _body.onInventoryChanged += onBodyInventoryChanged;

            if (_body.characterMotor)
            {
                _body.characterMotor.onHitGroundAuthority += onHitGroundAuthority;
            }

            if (_body.modelLocator)
            {
                _body.modelLocator.onModelChanged += refreshModelReference;
            }

            refreshModelReference(_body.modelLocator ? _body.modelLocator.modelTransform : null);
        }

        void OnDisable()
        {
            _body.onInventoryChanged -= onBodyInventoryChanged;

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
            recalculateStatsIfNeeded();

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

        void onBodyInventoryChanged()
        {
            MarkAllStatsDirty();

            void setItemBehavior<T>(bool enabled) where T : Behaviour
            {
                T itemBehavior = GetComponent<T>();
                bool alreadyEnabled = itemBehavior && itemBehavior.enabled;

                if (alreadyEnabled != enabled)
                {
                    if (itemBehavior)
                    {
                        itemBehavior.enabled = enabled;
                    }
                    else if (enabled)
                    {
                        gameObject.AddComponent<T>();
                    }
                }
            }

            if (NetworkServer.active)
            {
                setItemBehavior<MoveSpeedOnKillQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.MoveSpeedOnKill.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<AttackSpeedOnCritQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.AttackSpeedOnCrit.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<SprintOutOfCombatQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.SprintOutOfCombat.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<SprintArmorQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.SprintArmor.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<HealOnCritQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.HealOnCrit.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<EnergizedOnEquipmentUseItemBehavior>(ItemQualitiesContent.ItemQualityGroups.EnergizedOnEquipmentUse.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<BarrierOnOverHealQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.BarrierOnOverHeal.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<KillEliteFrenzyQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.KillEliteFrenzy.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<ArmorPlateQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.ArmorPlate.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<BoostAllStatsQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.BoostAllStats.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<IgniteOnKillQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.IgniteOnKill.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<MushroomVoidQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.MushroomVoid.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<FragileDamageBonusQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.FragileDamageBonus.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<MushroomQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.Mushroom.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<EquipmentMagazineQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.EquipmentMagazine.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<DuplicatorQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.Duplicator.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<FeatherQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.Feather.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<DronesDropDynamiteQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.DronesDropDynamite.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<DronesDropDynamiteDroneItemQualityItemBehavior>(_body.inventory.GetItemCountEffective(ItemQualitiesContent.Items.DronesDropDynamiteQualityDroneItem) > 0);
                setItemBehavior<ShieldBoosterQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.ShieldBooster.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<WarCryOnMultiKillQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.WarCryOnMultiKill.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
            }

            if (HasEffectiveAuthority)
            {
                setItemBehavior<SecondarySkillMagazineQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.SecondarySkillMagazine.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<UtilitySkillMagazineQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.UtilitySkillMagazine.GetItemCountsEffective(_body.inventory).TotalQualityCount > 0);
            }
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

        public void MarkAllStatsDirty()
        {
            _statsDirty = true;
        }

        void recalculateStatsIfNeeded()
        {
            if (_statsDirty)
            {
                _statsDirty = false;
                recalculateExtraStats();
            }
        }

        void recalculateExtraStats()
        {
            ItemQualityCounts crowbar = default;
            ItemQualityCounts executeLowHealthElite = default;
            ItemQualityCounts phasing = default;
            ItemQualityCounts jumpBoost = default;
            if (_body && _body.inventory)
            {
                crowbar = ItemQualitiesContent.ItemQualityGroups.Crowbar.GetItemCountsEffective(_body.inventory);
                executeLowHealthElite = ItemQualitiesContent.ItemQualityGroups.ExecuteLowHealthElite.GetItemCountsEffective(_body.inventory);
                phasing = ItemQualitiesContent.ItemQualityGroups.Phasing.GetItemCountsEffective(_body.inventory);
                jumpBoost = ItemQualitiesContent.ItemQualityGroups.JumpBoost.GetItemCountsEffective(_body.inventory);
            }

            float crowbarMinHealthFractionReduction = Util.ConvertAmplificationPercentageIntoReductionNormalized(amplificationNormal:
                (0.25f * crowbar.UncommonCount) +
                (0.43f * crowbar.RareCount) +
                (1.00f * crowbar.EpicCount) +
                (3.00f * crowbar.LegendaryCount));

            _crowbarMinHealthFraction = Mathf.Lerp(BaseCrowbarMinHealthFraction, BaseCrowbarMinHealthFraction * 0.5f, crowbarMinHealthFractionReduction);
            
            _executeBossHealthFraction = Util.ConvertAmplificationPercentageIntoReductionNormalized(amplificationNormal:
                (0.10f * executeLowHealthElite.UncommonCount) +
                (0.15f * executeLowHealthElite.RareCount) +
                (0.25f * executeLowHealthElite.EpicCount) +
                (0.40f * executeLowHealthElite.LegendaryCount));

            float stealthKitActivationThresholdIncrease = 1f;
            stealthKitActivationThresholdIncrease *= Mathf.Pow(1f - 0.10f, phasing.UncommonCount);
            stealthKitActivationThresholdIncrease *= Mathf.Pow(1f - 0.25f, phasing.RareCount);
            stealthKitActivationThresholdIncrease *= Mathf.Pow(1f - 0.50f, phasing.EpicCount);
            stealthKitActivationThresholdIncrease *= Mathf.Pow(1f - 0.75f, phasing.LegendaryCount);

            _stealthKitActivationThreshold = 1f - ((1f - HealthComponent.lowHealthFraction) * stealthKitActivationThresholdIncrease);

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

            _airControlBonus = airControlBonus;
        }

        void IOnIncomingDamageServerReceiver.OnIncomingDamageServer(DamageInfo damageInfo)
        {
            if (MasterExtraStatsTracker)
            {
                MasterExtraStatsTracker.OnIncomingDamageServer(damageInfo);
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
            _body.UpdateSingleTemporaryVisualEffect(ref _qualityDeathMarkEffectInstance, ItemQualitiesContent.Prefabs.DeathMarkQualityEffect, _body.radius, DeathMark.HasAnyQualityDeathMarkDebuff(_body));
            _body.UpdateSingleTemporaryVisualEffect(ref _sprintArmorStrongEffectInstance, SprintArmor.BucklerDefenseBigPrefab, _body.radius * 1.5f, _body.HasBuff(ItemQualitiesContent.Buffs.SprintArmorStrong));
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
                MarkAllStatsDirty();
            }
        }
    }
}
