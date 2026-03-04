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
            _hitlistMarkers = Instantiate(ItemQualitiesContent.Prefabs.HitlistMarkers, bottomRightClusterTransform);
            _hitlistMarkers.name = "HitlistMarkers";
        }

        void OnEnable()
        {
            setTargetBodyObject(_hud.targetBodyObject);

            HUD.onHudTargetChangedGlobal += onHudTargetChangedGlobal;
            BossDamageBonus.TicksChanged += updateTickVisual;
        }

        void OnDisable()
        {
            HUD.onHudTargetChangedGlobal -= onHudTargetChangedGlobal;
            BossDamageBonus.TicksChanged -= updateTickVisual;
            setTargetBodyObject(null);
        }

        public void updateTickVisual()
        {
            if (!_currentTargetBody.master.TryGetComponentCached(out CharacterMasterExtraStatsTracker masterExtraStats))
                return;
            if (_currentTargetBody.master.localPlayerAuthority)
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

            _currentTargetBodyObject = targetBodyObject;
            _currentTargetBody = _currentTargetBodyObject ? _currentTargetBodyObject.GetComponent<CharacterBody>() : null;
            updateTickVisual();
        }
    }
}

