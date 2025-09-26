using HG;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public class CharacterBodyExtraStatsTracker : NetworkBehaviour
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

        uint _lastMoneyValue;

        bool _statsDirty;

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

        float _watchBreakThreshold = HealthComponent.lowHealthFraction;
        public float WatchBreakThreshold
        {
            get
            {
                recalculateStatsIfNeeded();
                return _watchBreakThreshold;
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

        [SyncVar(hook = nameof(hookSetSlugOutOfDanger))]
        bool _slugOutOfDanger;
        public bool SlugOutOfDanger => _slugOutOfDanger;

        [SyncVar(hook = nameof(hookSetShieldOutOfDanger))]
        bool _shieldOutOfDanger;
        public bool ShieldOutOfDanger => _shieldOutOfDanger;

        public bool MushroomActiveServer { get; private set; }

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
        }

        void OnEnable()
        {
            recalculateExtraStats();
            _body.onInventoryChanged += onBodyInventoryChanged;
            _body.onSkillActivatedAuthority += onSkillActivatedAuthority;

            EquipmentSlot.onServerEquipmentActivated += onServerEquipmentActivated;
        }

        void OnDisable()
        {
            _body.onInventoryChanged -= onBodyInventoryChanged;
            _body.onSkillActivatedAuthority -= onSkillActivatedAuthority;

            EquipmentSlot.onServerEquipmentActivated -= onServerEquipmentActivated;
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
            }
        }

        void onBodyInventoryChanged()
        {
            MarkAllStatsDirty();
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
            if (_body && _body.inventory)
            {
                slug = ItemQualitiesContent.ItemQualityGroups.HealWhileSafe.GetItemCounts(_body.inventory);
                crowbar = ItemQualitiesContent.ItemQualityGroups.Crowbar.GetItemCounts(_body.inventory);
                personalShield = ItemQualitiesContent.ItemQualityGroups.PersonalShield.GetItemCounts(_body.inventory);
                barrierOnKill = ItemQualitiesContent.ItemQualityGroups.BarrierOnKill.GetItemCounts(_body.inventory);
                fragileDamageBonus = ItemQualitiesContent.ItemQualityGroups.FragileDamageBonus.GetItemCounts(_body.inventory);
                mushroom = ItemQualitiesContent.ItemQualityGroups.Mushroom.GetItemCounts(_body.inventory);
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

            float watchBreakThresholdReduction = 1f;
            watchBreakThresholdReduction += 0.10f * fragileDamageBonus.UncommonCount;
            watchBreakThresholdReduction += 0.25f * fragileDamageBonus.RareCount;
            watchBreakThresholdReduction += 1.00f * fragileDamageBonus.EpicCount;
            watchBreakThresholdReduction += 3.00f * fragileDamageBonus.LegendaryCount;

            _watchBreakThreshold = HealthComponent.lowHealthFraction / watchBreakThresholdReduction;

            float mushroomNotMovingStopwatchThresholdReduction = 1f;
            mushroomNotMovingStopwatchThresholdReduction += 0.18f * mushroom.UncommonCount;
            mushroomNotMovingStopwatchThresholdReduction += 0.33f * mushroom.RareCount;
            mushroomNotMovingStopwatchThresholdReduction += 0.66f * mushroom.EpicCount;
            mushroomNotMovingStopwatchThresholdReduction += 1.50f * mushroom.LegendaryCount;

            _mushroomNotMovingStopwatchThreshold = BaseMushroomNotMovingStopwatchThreshold / mushroomNotMovingStopwatchThresholdReduction;
        }

        void onSkillActivatedAuthority(GenericSkill skill)
        {
            if (_body.skillLocator && _body.skillLocator.secondary == skill)
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
    }
}
