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

        [SyncVar(hook = nameof(hookSetSlugOutOfDanger))]
        bool _slugOutOfDanger;
        public bool SlugOutOfDanger => _slugOutOfDanger;

        [SyncVar(hook = nameof(hookSetShieldOutOfDanger))]
        bool _shieldOutOfDanger;
        public bool ShieldOutOfDanger => _shieldOutOfDanger;

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
        }

        void OnEnable()
        {
            recalculateStats();
            _body.onInventoryChanged += onBodyInventoryChanged;
        }

        void OnDisable()
        {
            _body.onInventoryChanged -= onBodyInventoryChanged;
        }

        void Update()
        {
            if (NetworkServer.active)
            {
                recalculateStatsIfNeeded();

                _slugOutOfDanger = _body && _body.outOfDangerStopwatch >= _slugOutOfDangerDelay;
                _shieldOutOfDanger = _body && _body.outOfDangerStopwatch >= _shieldOutOfDangerDelay;
            }
        }

        void onBodyInventoryChanged()
        {
            _statsDirty = true;
        }

        void recalculateStatsIfNeeded()
        {
            if (_statsDirty)
            {
                _statsDirty = false;
                recalculateStats();
            }
        }

        void recalculateStats()
        {
            ItemQualityCounts slug = default;
            ItemQualityCounts crowbar = default;
            ItemQualityCounts personalShield = default;
            ItemQualityCounts barrierOnKill = default;
            if (_body && _body.inventory)
            {
                slug = ItemQualitiesContent.ItemQualityGroups.HealWhileSafe.GetItemCounts(_body.inventory);
                crowbar = ItemQualitiesContent.ItemQualityGroups.Crowbar.GetItemCounts(_body.inventory);
                personalShield = ItemQualitiesContent.ItemQualityGroups.PersonalShield.GetItemCounts(_body.inventory);
                barrierOnKill = ItemQualitiesContent.ItemQualityGroups.BarrierOnKill.GetItemCounts(_body.inventory);
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
