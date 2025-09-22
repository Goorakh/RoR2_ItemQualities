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

        float _slugOutOfDangerDelay = CharacterBody.outOfDangerDelay;

        [SyncVar(hook = nameof(hookSetSlugActive))]
        bool _slugActive;
        public bool SlugActive => _slugActive;

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
                _slugActive = _body && _body.outOfDangerStopwatch >= _slugOutOfDangerDelay;
            }
        }

        void onBodyInventoryChanged()
        {
            recalculateStats();
        }

        void recalculateStats()
        {
            int slugUncommonCount = 0;
            int slugRareCount = 0;
            int slugEpicCount = 0;
            int slugLegendaryCount = 0;
            if (_body && _body.inventory)
            {
                slugUncommonCount = _body.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.HealWhileSafe.UncommonItemIndex);
                slugRareCount = _body.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.HealWhileSafe.RareItemIndex);
                slugEpicCount = _body.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.HealWhileSafe.EpicItemIndex);
                slugLegendaryCount = _body.inventory.GetItemCount(ItemQualitiesContent.ItemQualityGroups.HealWhileSafe.LegendaryItemIndex);
            }

            float slugOutOfDangerDelayDecrease = 1f;
            slugOutOfDangerDelayDecrease += 0.10f * slugUncommonCount;
            slugOutOfDangerDelayDecrease += 0.25f * slugRareCount;
            slugOutOfDangerDelayDecrease += 1.00f * slugEpicCount;
            slugOutOfDangerDelayDecrease += 4.00f * slugLegendaryCount;

            _slugOutOfDangerDelay = CharacterBody.outOfDangerDelay / slugOutOfDangerDelayDecrease;
        }

        void hookSetSlugActive(bool slugActive)
        {
            _slugActive = slugActive;
            _body.MarkAllStatsDirty();
        }
    }
}
