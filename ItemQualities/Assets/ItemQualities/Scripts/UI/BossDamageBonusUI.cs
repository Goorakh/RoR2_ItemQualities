using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace ItemQualities.UI
{
    public sealed class BossDamageBonusUI : MonoBehaviour
    {
        public RectTransform MarkersRoot;

        public GameObject MarkerSectionPrefab;

        private UIElementAllocator<BossDamageBonusTickController> _tickAllocator;

        private int _maxTicksPerSection;

        private HUD _hud;

        private CharacterMaster _targetMaster;
        private CharacterMasterExtraStatsTracker _targetMasterStats;

        private void Awake()
        {
            _hud = GetComponentInParent<HUD>();

            _tickAllocator = new UIElementAllocator<BossDamageBonusTickController>(MarkersRoot, MarkerSectionPrefab);

            _maxTicksPerSection = MarkerSectionPrefab.GetComponent<BossDamageBonusTickController>().NumberSprites.Length;
        }

        private void OnEnable()
        {
            setTargetMaster(_hud.targetMaster);

            HUD.onHudTargetChangedGlobal += onHudTargetChangedGlobal;
        }

        private void OnDisable()
        {
            HUD.onHudTargetChangedGlobal -= onHudTargetChangedGlobal;

            setTargetMaster(null);
        }

        private void onHudTargetChangedGlobal(HUD hud)
        {
            if (hud != _hud)
                return;

            setTargetMaster(hud.targetMaster);
        }

        private void setTargetMaster(CharacterMaster master)
        {
            if (_targetMaster == master)
                return;

            if (_targetMasterStats)
            {
                _targetMasterStats.OnBossDamageBonusTicksChanged -= onBossDamageBonusTicksChanged;
            }

            if (_targetMaster && _targetMaster.inventory)
            {
                _targetMaster.inventory.onInventoryChanged -= onTargetInventoryChanged;
            }

            _targetMaster = master;
            _targetMasterStats = master ? master.GetComponentCached<CharacterMasterExtraStatsTracker>() : null;

            if (_targetMasterStats)
            {
                _targetMasterStats.OnBossDamageBonusTicksChanged += onBossDamageBonusTicksChanged;
            }

            if (_targetMaster && _targetMaster.inventory)
            {
                _targetMaster.inventory.onInventoryChanged += onTargetInventoryChanged;
            }

            refreshTicks();
        }

        private void onBossDamageBonusTicksChanged(CharacterMasterExtraStatsTracker masterExtraStats)
        {
            refreshTicks();
        }

        private void onTargetInventoryChanged()
        {
            refreshTicks();
        }

        private void refreshTicks()
        {
            ItemQualityCounts bossDamageBonus = ItemQualityCounts.zero;
            if (_targetMaster && _targetMaster.inventory)
            {
                bossDamageBonus = _targetMaster.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BossDamageBonus);
            }

            bool shouldShowTicks = bossDamageBonus.TotalQualityCount > 0;

            int ticksToDisplay = shouldShowTicks && _targetMasterStats ? _targetMasterStats.BossDamageBonusTicks : 0;

            int tickSections = ticksToDisplay > 0 ? HGMath.IntDivCeil(ticksToDisplay, _maxTicksPerSection) : 0;

            _tickAllocator.AllocateElements(tickSections);
            for (int i = 0; i < tickSections; i++)
            {
                BossDamageBonusTickController markerController = _tickAllocator.elements[i];
                markerController.DisplayedNumber = Mathf.Min(_maxTicksPerSection, _targetMasterStats.BossDamageBonusTicks - (_maxTicksPerSection * i));
            }
        }
    }
}

