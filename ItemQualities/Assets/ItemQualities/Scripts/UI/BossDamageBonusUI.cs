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

        UIElementAllocator<BossDamageBonusTickController> _tickAllocator;

        int _maxTicksPerSection;

        HUD _hud;

        CharacterMaster _targetMaster;
        CharacterMasterExtraStatsTracker _targetMasterStats;

        void Awake()
        {
            _hud = GetComponentInParent<HUD>();

            _tickAllocator = new UIElementAllocator<BossDamageBonusTickController>(MarkersRoot, MarkerSectionPrefab);

            _maxTicksPerSection = MarkerSectionPrefab.GetComponent<BossDamageBonusTickController>().NumberSprites.Length;
        }

        void OnEnable()
        {
            setTargetMaster(_hud.targetMaster);

            HUD.onHudTargetChangedGlobal += onHudTargetChangedGlobal;
        }

        void OnDisable()
        {
            HUD.onHudTargetChangedGlobal -= onHudTargetChangedGlobal;

            setTargetMaster(null);
        }

        void onHudTargetChangedGlobal(HUD hud)
        {
            if (hud != _hud)
                return;

            setTargetMaster(hud.targetMaster);
        }

        void setTargetMaster(CharacterMaster master)
        {
            if (_targetMaster == master)
                return;

            if (_targetMasterStats)
            {
                _targetMasterStats.OnBossDamageBonusTicksChanged -= onBossDamageBonusTicksChanged;
            }

            _targetMaster = master;
            _targetMasterStats = master ? master.GetComponentCached<CharacterMasterExtraStatsTracker>() : null;

            if (_targetMasterStats)
            {
                _targetMasterStats.OnBossDamageBonusTicksChanged += onBossDamageBonusTicksChanged;
            }

            refreshTicks();
        }

        void onBossDamageBonusTicksChanged(CharacterMasterExtraStatsTracker masterExtraStats)
        {
            refreshTicks();
        }

        void refreshTicks()
        {
            int sections = _targetMasterStats && _targetMasterStats.BossDamageBonusTicks > 0 ? HGMath.IntDivCeil(_targetMasterStats.BossDamageBonusTicks, _maxTicksPerSection) : 0;
            _tickAllocator.AllocateElements(sections);

            for (int i = 0; i < sections; i++)
            {
                BossDamageBonusTickController markerController = _tickAllocator.elements[i];
                markerController.DisplayedNumber = Mathf.Min(_maxTicksPerSection, _targetMasterStats.BossDamageBonusTicks - (_maxTicksPerSection * i));
            }
        }
    }
}

