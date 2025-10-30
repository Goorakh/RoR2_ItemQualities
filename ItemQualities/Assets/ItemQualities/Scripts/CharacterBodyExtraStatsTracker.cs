using HG;
using ItemQualities.Items;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public class CharacterBodyExtraStatsTracker : NetworkBehaviour, IOnIncomingDamageServerReceiver
    {
        [SystemInitializer(typeof(BodyCatalog))]
        static void Init()
        {
            foreach (GameObject bodyPrefab in BodyCatalog.allBodyPrefabs)
            {
                bodyPrefab.EnsureComponent<CharacterBodyExtraStatsTracker>();
            }
        }

        CharacterBody _body;

        MemoizedGetComponent<CharacterMasterExtraStatsTracker> _masterExtraStatsComponent;

        uint _lastMoneyValue;

        bool _statsDirty;

        TemporaryVisualEffect _qualityDeathMarkEffectInstance;
        TemporaryVisualEffect _sprintArmorStrongEffectInstance;

        TemporaryOverlayInstance _healCritBoostOverlay;

        float _timeSinceLastUtilitySkillRechargeAuthority = float.PositiveInfinity;

        public ItemQualityCounts LastExtraStatsOnLevelUpCounts = default;

        float _slugOutOfDangerDelay = CharacterBody.outOfDangerDelay;
        public float SlugOutOfDangerDelay
        {
            get
            {
                recalculateStatsIfNeeded();
                return _slugOutOfDangerDelay;
            }
        }

        float _shieldOutOfDangerDelay = CharacterBody.outOfDangerDelay;
        public float ShieldOutOfDangerDelay
        {
            get
            {
                recalculateStatsIfNeeded();
                return _shieldOutOfDangerDelay;
            }
        }

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

        float _barrierDecayRateMultiplier = 1f;
        public float BarrierDecayRateMultiplier
        {
            get
            {
                recalculateStatsIfNeeded();
                return _barrierDecayRateMultiplier;
            }
        }

        const float BaseMushroomNotMovingStopwatchThreshold = 1f;
        float _mushroomNotMovingStopwatchThreshold = BaseMushroomNotMovingStopwatchThreshold;
        public float MushroomNotMovingStopwatchThreshold
        {
            get
            {
                recalculateStatsIfNeeded();
                return _mushroomNotMovingStopwatchThreshold;
            }
        }

        public int WarCryOnMultiKill_MultiKillCount { get; private set; }
        float _warCryOnMultiKill_MultiKillTimer = 0;
        float _warCryOnMultiKill_MultiKillDuration = CharacterBody.multiKillMaxInterval;
        public float WarCryOnMultiKill_MultiKillDuration
        {
            get
            {
                recalculateStatsIfNeeded();
                return _warCryOnMultiKill_MultiKillDuration;
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

        public bool HasEffectiveAuthority => Util.HasEffectiveAuthority(gameObject);

        [SyncVar(hook = nameof(hookSetSlugOutOfDanger))]
        bool _slugOutOfDanger;
        public bool SlugOutOfDanger => _slugOutOfDanger;

        [SyncVar(hook = nameof(hookSetShieldOutOfDanger))]
        bool _shieldOutOfDanger;
        public bool ShieldOutOfDanger => _shieldOutOfDanger;

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

        public bool MushroomActiveServer { get; private set; }

        public bool HasHadAnyQualityDeathMarkDebuffServer { get; private set; }

        public CharacterMasterExtraStatsTracker MasterExtraStatsTracker => _masterExtraStatsComponent.Get(_body.masterObject);

        public event Action<DamageInfo> OnIncomingDamageServer;

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
        }

        void OnEnable()
        {
            recalculateExtraStats();
            _body.onInventoryChanged += onBodyInventoryChanged;
            _body.onSkillActivatedAuthority += onSkillActivatedAuthority;

            if (_body.characterMotor)
            {
                _body.characterMotor.onHitGroundAuthority += onHitGroundAuthority;
            }

            EquipmentSlot.onServerEquipmentActivated += onServerEquipmentActivated;
            GenericSkillHooks.OnSkillRechargeAuthority += onSkillRechargeAuthority;
        }

        void OnDisable()
        {
            _body.onInventoryChanged -= onBodyInventoryChanged;
            _body.onSkillActivatedAuthority -= onSkillActivatedAuthority;

            if (_body.characterMotor)
            {
                _body.characterMotor.onHitGroundAuthority -= onHitGroundAuthority;
            }

            EquipmentSlot.onServerEquipmentActivated -= onServerEquipmentActivated;
            GenericSkillHooks.OnSkillRechargeAuthority -= onSkillRechargeAuthority;
        }

        void FixedUpdate()
        {
            CharacterMaster master = _body ? _body.master : null;

            uint currentMoneyValue = master ? master.money : 0;
            if (currentMoneyValue != _lastMoneyValue)
            {
                _body.MarkAllStatsDirty();
                _lastMoneyValue = currentMoneyValue;
            }

            recalculateStatsIfNeeded();

            if (NetworkServer.active)
            {
                _slugOutOfDanger = _body && _body.outOfDangerStopwatch >= _slugOutOfDangerDelay;
                _shieldOutOfDanger = _body && _body.outOfDangerStopwatch >= _shieldOutOfDangerDelay;
                MushroomActiveServer = _body && _body.notMovingStopwatch >= _mushroomNotMovingStopwatchThreshold;

                if (!HasHadAnyQualityDeathMarkDebuffServer && DeathMark.HasAnyQualityDeathMarkDebuff(_body))
                {
                    HasHadAnyQualityDeathMarkDebuffServer = true;
                }
            }

            if (HasEffectiveAuthority)
            {
                _timeSinceLastUtilitySkillRechargeAuthority += Time.fixedDeltaTime;

                if (QuailJumpComboAuthority > 0 && !IsPerformingQuailJump && LastQuailLandTimeAuthority.timeSince > 0.1f)
                {
                    QuailJumpComboAuthority = 0;
                }
            }

            updateOverlays();
        }

        void Update()
        {
            if (NetworkServer.active)
            {
                if (WarCryOnMultiKill_MultiKillCount > 0)
                {
                    _warCryOnMultiKill_MultiKillTimer += Time.deltaTime;
                    if (_warCryOnMultiKill_MultiKillTimer >= _warCryOnMultiKill_MultiKillDuration)
                    {
                        _warCryOnMultiKill_MultiKillTimer = 0f;
                        WarCryOnMultiKill_MultiKillCount = 0;
                    }
                }
            }
        }

        [Server]
        public void AddMultiKill(int kills)
        {
            _warCryOnMultiKill_MultiKillTimer = 0f;
            WarCryOnMultiKill_MultiKillCount += kills;
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
                setItemBehavior<MoveSpeedOnKillQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.MoveSpeedOnKill.GetItemCounts(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<AttackSpeedOnCritQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.AttackSpeedOnCrit.GetItemCounts(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<SprintOutOfCombatQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.SprintOutOfCombat.GetItemCounts(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<SprintArmorQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.SprintArmor.GetItemCounts(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<HealOnCritQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.HealOnCrit.GetItemCounts(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<EnergizedOnEquipmentUseItemBehavior>(ItemQualitiesContent.ItemQualityGroups.EnergizedOnEquipmentUse.GetItemCounts(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<BarrierOnOverHealQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.BarrierOnOverHeal.GetItemCounts(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<KillEliteFrenzyQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.KillEliteFrenzy.GetItemCounts(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<ArmorPlateQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.ArmorPlate.GetItemCounts(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<BoostAllStatsQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.BoostAllStats.GetItemCounts(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<MushroomVoidQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.MushroomVoid.GetItemCounts(_body.inventory).TotalQualityCount > 0);
                setItemBehavior<FragileDamageBonusQualityItemBehavior>(ItemQualitiesContent.ItemQualityGroups.FragileDamageBonus.GetItemCounts(_body.inventory).TotalQualityCount > 0);
            }
        }

        void updateOverlays()
        {
            CharacterModel characterModel = null;
            if (_body && _body.modelLocator && _body.modelLocator.modelTransform)
            {
                characterModel = _body.modelLocator.modelTransform.GetComponent<CharacterModel>();
            }

            void setOverlay(ref TemporaryOverlayInstance overlayInstance, Material material, bool active)
            {
                if (!material)
                    return;

                if (!characterModel)
                {
                    active = false;
                }

                bool overlayActive = overlayInstance != null && overlayInstance.assignedCharacterModel == characterModel;
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

                    overlayInstance.AddToCharacterModel(characterModel);
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
            ItemQualityCounts slug = default;
            ItemQualityCounts crowbar = default;
            ItemQualityCounts personalShield = default;
            ItemQualityCounts barrierOnKill = default;
            ItemQualityCounts fragileDamageBonus = default;
            ItemQualityCounts mushroom = default;
            ItemQualityCounts warCryOnMultiKill = default;
            ItemQualityCounts executeLowHealthElite = default;
            ItemQualityCounts phasing = default;
            ItemQualityCounts jumpBoost = default;
            if (_body && _body.inventory)
            {
                slug = ItemQualitiesContent.ItemQualityGroups.HealWhileSafe.GetItemCounts(_body.inventory);
                crowbar = ItemQualitiesContent.ItemQualityGroups.Crowbar.GetItemCounts(_body.inventory);
                personalShield = ItemQualitiesContent.ItemQualityGroups.PersonalShield.GetItemCounts(_body.inventory);
                barrierOnKill = ItemQualitiesContent.ItemQualityGroups.BarrierOnKill.GetItemCounts(_body.inventory);
                fragileDamageBonus = ItemQualitiesContent.ItemQualityGroups.FragileDamageBonus.GetItemCounts(_body.inventory);
                mushroom = ItemQualitiesContent.ItemQualityGroups.Mushroom.GetItemCounts(_body.inventory);
                warCryOnMultiKill = ItemQualitiesContent.ItemQualityGroups.WarCryOnMultiKill.GetItemCounts(_body.inventory);
                executeLowHealthElite = ItemQualitiesContent.ItemQualityGroups.ExecuteLowHealthElite.GetItemCounts(_body.inventory);
                phasing = ItemQualitiesContent.ItemQualityGroups.Phasing.GetItemCounts(_body.inventory);
                jumpBoost = ItemQualitiesContent.ItemQualityGroups.JumpBoost.GetItemCounts(_body.inventory);
            }

            float slugOutOfDangerDelayReduction = 1f;
            slugOutOfDangerDelayReduction += 0.18f * slug.UncommonCount;
            slugOutOfDangerDelayReduction += 0.33f * slug.RareCount;
            slugOutOfDangerDelayReduction += 1.00f * slug.EpicCount;
            slugOutOfDangerDelayReduction += 3.00f * slug.LegendaryCount;

            _slugOutOfDangerDelay = CharacterBody.outOfDangerDelay / slugOutOfDangerDelayReduction;

            float crowbarMinHealthFractionReduction = Util.ConvertAmplificationPercentageIntoReductionNormalized(amplificationNormal:
                (0.25f * crowbar.UncommonCount) +
                (0.43f * crowbar.RareCount) +
                (1.00f * crowbar.EpicCount) +
                (3.00f * crowbar.LegendaryCount));

            _crowbarMinHealthFraction = Mathf.Lerp(BaseCrowbarMinHealthFraction, BaseCrowbarMinHealthFraction * 0.5f, crowbarMinHealthFractionReduction);

            float shieldOutOfDangerDelayReduction = 1f;
            shieldOutOfDangerDelayReduction += 0.10f * personalShield.UncommonCount;
            shieldOutOfDangerDelayReduction += 0.25f * personalShield.RareCount;
            shieldOutOfDangerDelayReduction += 1.00f * personalShield.EpicCount;
            shieldOutOfDangerDelayReduction += 3.00f * personalShield.LegendaryCount;

            _shieldOutOfDangerDelay = CharacterBody.outOfDangerDelay / shieldOutOfDangerDelayReduction;

            float barrierDecayRateReduction = 1f;
            barrierDecayRateReduction += 0.10f * barrierOnKill.UncommonCount;
            barrierDecayRateReduction += 0.25f * barrierOnKill.RareCount;
            barrierDecayRateReduction += 1.00f * barrierOnKill.EpicCount;
            barrierDecayRateReduction += 3.00f * barrierOnKill.LegendaryCount;

            _barrierDecayRateMultiplier = 1f / barrierDecayRateReduction;

            float mushroomNotMovingStopwatchThresholdReduction = 1f;
            mushroomNotMovingStopwatchThresholdReduction += 0.18f * mushroom.UncommonCount;
            mushroomNotMovingStopwatchThresholdReduction += 0.33f * mushroom.RareCount;
            mushroomNotMovingStopwatchThresholdReduction += 0.66f * mushroom.EpicCount;
            mushroomNotMovingStopwatchThresholdReduction += 1.50f * mushroom.LegendaryCount;

            _mushroomNotMovingStopwatchThreshold = BaseMushroomNotMovingStopwatchThreshold / mushroomNotMovingStopwatchThresholdReduction;

            float warCryOnMultiKill_MultiKillDurationMult = 1f;
            warCryOnMultiKill_MultiKillDurationMult += 0.3f * warCryOnMultiKill.UncommonCount;
            warCryOnMultiKill_MultiKillDurationMult += 0.7f * warCryOnMultiKill.RareCount;
            warCryOnMultiKill_MultiKillDurationMult += 1.5f * warCryOnMultiKill.EpicCount;
            warCryOnMultiKill_MultiKillDurationMult += 2.5f * warCryOnMultiKill.LegendaryCount;

            _warCryOnMultiKill_MultiKillDuration = CharacterBody.multiKillMaxInterval * warCryOnMultiKill_MultiKillDurationMult;

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
            CharacterMasterExtraStatsTracker masterExtraStats = _masterExtraStatsComponent.Get(_body.masterObject);
            if (masterExtraStats)
            {
                masterExtraStats.OnIncomingDamageServer(damageInfo);
            }

            OnIncomingDamageServer?.Invoke(damageInfo);
        }

        void onSkillActivatedAuthority(GenericSkill skill)
        {
            if (!_body.skillLocator || !skill)
                return;

            if (_body.skillLocator.secondary == skill)
            {
                ItemQualityCounts secondarySkillMagazine = ItemQualitiesContent.ItemQualityGroups.SecondarySkillMagazine.GetItemCounts(_body.inventory);

                float freeRestockChance = (5f * secondarySkillMagazine.UncommonCount) +
                                          (15f * secondarySkillMagazine.RareCount) +
                                          (35f * secondarySkillMagazine.EpicCount) +
                                          (60f * secondarySkillMagazine.LegendaryCount);

                if (Util.CheckRoll(Util.ConvertAmplificationPercentageIntoReductionPercentage(freeRestockChance), _body.master))
                {
                    skill.AddOneStock();
                }
            }

            if (_body.skillLocator.utility == skill)
            {
                ItemQualityCounts utilitySkillMagazine = ItemQualitiesContent.ItemQualityGroups.UtilitySkillMagazine.GetItemCounts(_body.inventory);

                if (utilitySkillMagazine.TotalQualityCount > 0)
                {
                    float cooldownRefundWindow = 0.1f;

                    float cooldownReductionWindow = cooldownRefundWindow + 0.2f;

                    float remainingCooldownMultiplier = Mathf.Pow(1f - 0.1f, utilitySkillMagazine.UncommonCount) *
                                                        Mathf.Pow(1f - 0.2f, utilitySkillMagazine.RareCount) *
                                                        Mathf.Pow(1f - 0.3f, utilitySkillMagazine.EpicCount) *
                                                        Mathf.Pow(1f - 0.5f, utilitySkillMagazine.LegendaryCount);

                    if (_timeSinceLastUtilitySkillRechargeAuthority <= cooldownRefundWindow)
                    {
                        skill.AddOneStock();
                    }
                    else if (_timeSinceLastUtilitySkillRechargeAuthority <= cooldownReductionWindow)
                    {
                        skill.rechargeStopwatch += skill.cooldownRemaining * (1f - remainingCooldownMultiplier);
                    }
                }
            }
        }

        void onSkillRechargeAuthority(GenericSkill skill)
        {
            if (!_body.skillLocator || !skill)
                return;

            if (_body.skillLocator.utility == skill)
            {
                _timeSinceLastUtilitySkillRechargeAuthority = 0f;
            }
        }

        void onServerEquipmentActivated(EquipmentSlot equipmentSlot, EquipmentIndex equipmentIndex)
        {
            if (!_body || !_body.inventory || _body.equipmentSlot != equipmentSlot || equipmentIndex == EquipmentIndex.None)
                return;

            ItemQualityCounts equipmentMagazine = ItemQualitiesContent.ItemQualityGroups.EquipmentMagazine.GetItemCounts(_body.inventory);

            float freeRestockChance = (10f * equipmentMagazine.UncommonCount) +
                                      (20f * equipmentMagazine.RareCount) +
                                      (35f * equipmentMagazine.EpicCount) +
                                      (60f * equipmentMagazine.LegendaryCount);

            if (Util.CheckRoll(Util.ConvertAmplificationPercentageIntoReductionPercentage(freeRestockChance), _body.master))
            {
                _body.inventory.RestockEquipmentCharges(equipmentSlot.activeEquipmentSlot, 1);
            }
        }

        void onHitGroundAuthority(ref CharacterMotor.HitGroundInfo hitGroundInfo)
        {
            if (IsPerformingQuailJump)
            {
                LastQuailLandTimeAuthority = Run.FixedTimeStamp.now;
                IsPerformingQuailJump = false;
            }
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

        void hookSetSlugOutOfDanger(bool slugOutOfDanger)
        {
            bool changed = _slugOutOfDanger != slugOutOfDanger;
            _slugOutOfDanger = slugOutOfDanger;

            if (changed)
            {
                _body.MarkAllStatsDirty();
            }
        }

        void hookSetShieldOutOfDanger(bool shieldOutOfDanger)
        {
            bool changed = _shieldOutOfDanger != shieldOutOfDanger;
            _shieldOutOfDanger = shieldOutOfDanger;

            if (changed)
            {
                _body.MarkAllStatsDirty();

                if (_shieldOutOfDanger && _body.healthComponent.shield < _body.healthComponent.fullShield)
                {
                    Util.PlaySound("Play_item_proc_personal_shield_recharge", gameObject);
                }
            }
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
