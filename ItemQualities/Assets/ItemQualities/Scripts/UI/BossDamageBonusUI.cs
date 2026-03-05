using ItemQualities.Items;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.UI;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;
using UnityEngine.UI;

namespace ItemQualities
{
    public sealed class BossDamageBonusUI : MonoBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_UI.HUDSimple_prefab).OnSuccess(hudPrefab =>
            {
                hudPrefab.AddComponent<BossDamageBonusUI>();
            });
        }

        HUD _hud;
        GameObject _hitlistMarkers;

        CharacterBody _currentTargetBody;
        GameObject _currentTargetBodyObject;

        void Awake()
        {
            _hud = GetComponent<HUD>();
        }

        void Start()
        {
            Transform bottomRightClusterTransform = GetComponent<ChildLocator>().FindChild("BottomRightCluster");
            _hitlistMarkers = Instantiate(ItemQualitiesContent.Prefabs.HitlistMarkersUI, bottomRightClusterTransform);
            _hitlistMarkers.name = "HitlistMarkers";
        }

        void OnEnable()
        {
            setTargetBodyObject(_hud.targetBodyObject);

            HUD.onHudTargetChangedGlobal += onHudTargetChangedGlobal;
            if (_currentTargetBody && _currentTargetBody.master && _currentTargetBody.master.TryGetComponentCached(out CharacterMasterExtraStatsTracker masterExtraStats)) {
                masterExtraStats.BossDamageBonusTicksChanged += updateTickVisual;
            }
        }

        void OnDisable()
        {
            HUD.onHudTargetChangedGlobal -= onHudTargetChangedGlobal;
            if (_currentTargetBody && _currentTargetBody.master && _currentTargetBody.master.TryGetComponentCached(out CharacterMasterExtraStatsTracker masterExtraStats))
            {
                masterExtraStats.BossDamageBonusTicksChanged -= updateTickVisual;
            }
            setTargetBodyObject(null);
        }

        public void updateTickVisual(CharacterMasterExtraStatsTracker masterExtraStats)
        {
            ChildLocator childLocator = _hitlistMarkers.GetComponent<ChildLocator>();
            if (!childLocator)
                return;

            for (int i = 0; i < (float)masterExtraStats.BossDamageBonusTicks / 5; i++)
            {
                Transform child = childLocator.FindChild(i);
                child.gameObject.SetActive(true);
                Image image = child.GetComponent<Image>();
                Sprite sprite = (masterExtraStats.BossDamageBonusTicks - 5 * i) switch
                {
                    1 => ItemQualitiesContent.Sprites.hitlistTick_1,
                    2 => ItemQualitiesContent.Sprites.hitlistTick_2,
                    3 => ItemQualitiesContent.Sprites.hitlistTick_3,
                    4 => ItemQualitiesContent.Sprites.hitlistTick_4,
                    _ => ItemQualitiesContent.Sprites.hitlistTick_5,
                };
                image.sprite = sprite;
            }
        }

        void onHudTargetChangedGlobal(HUD hud)
        {
            if (hud != _hud)
                return;

            setTargetBodyObject(hud.targetBodyObject);
        }

        void setTargetBodyObject(GameObject targetBodyObject)
        {
            if (targetBodyObject == _currentTargetBodyObject)
                return;

            if (_currentTargetBody && _currentTargetBody.master && _currentTargetBody.master.TryGetComponentCached(out CharacterMasterExtraStatsTracker masterExtraStats))
            {
                masterExtraStats.BossDamageBonusTicksChanged -= updateTickVisual;
            }
            _currentTargetBodyObject = targetBodyObject;
            _currentTargetBody = _currentTargetBodyObject ? _currentTargetBodyObject.GetComponent<CharacterBody>() : null;
            
            if (_currentTargetBody && _currentTargetBody.master && _currentTargetBody.master.TryGetComponentCached(out masterExtraStats)) {
                masterExtraStats.BossDamageBonusTicksChanged += updateTickVisual;
                updateTickVisual(masterExtraStats);
            }
        }
    }
}

