using HG;
using ItemQualities.Items;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.UI;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;

namespace ItemQualities.UI
{
    public sealed class DuplicatorItemShareUIController : MonoBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_UI.HUDSimple_prefab).OnSuccess(hudPrefab =>
            {
                hudPrefab.AddComponent<DuplicatorItemShareUIController>();
            });
        }

        HUD _hud;
        ChildLocator _childLocator;

        GameObject _itemShareInventoryDisplayRoot;
        ItemInventoryDisplay _itemShareInventoryDisplay;

        CharacterMaster _currentTargetMaster;
        QualityDuplicatorMinionInventoryController _currentTargetMinionInventoryController;

        void Awake()
        {
            _hud = GetComponent<HUD>();
            _childLocator = GetComponent<ChildLocator>();

            Transform leftClusterTransform = _childLocator.FindChild("LeftCluster");

            _itemShareInventoryDisplayRoot = Instantiate(_hud.itemInventoryDisplay.gameObject, leftClusterTransform);
            _itemShareInventoryDisplayRoot.name = "DuplicatorItemShareInventoryDisplay";
            _itemShareInventoryDisplay = _itemShareInventoryDisplayRoot.GetComponent<ItemInventoryDisplay>();

            RectTransform itemShareDisplayTransform = _itemShareInventoryDisplayRoot.GetComponent<RectTransform>();
            itemShareDisplayTransform.anchorMin = Vector2.zero;
            itemShareDisplayTransform.anchorMax = Vector2.one;
            itemShareDisplayTransform.sizeDelta = new Vector2(-120f, 0f);
            itemShareDisplayTransform.anchoredPosition = new Vector2(-90f, 0f);

            _itemShareInventoryDisplay.maxHeight = itemShareDisplayTransform.rect.height;

            // Will be enabled whenever an inventory is found
            _itemShareInventoryDisplayRoot.SetActive(false);
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
            if (!ReferenceEquals(hud, _hud))
                return;

            setTargetMaster(hud.targetMaster);
        }

        void setTargetMaster(CharacterMaster newTargetMaster)
        {
            if (ReferenceEquals(_currentTargetMaster, newTargetMaster))
                return;

            if (!ReferenceEquals(_currentTargetMaster, null))
            {
                QualityDuplicatorMinionInventoryController.OnOwnerDiscoveredGlobal -= onDuplicatorMinionInventoryOwnerDiscoveredGlobal;
                QualityDuplicatorMinionInventoryController.OnOwnerLostGlobal -= onDuplicatorMinionInventoryOwnerLostGlobal;
            }

            _currentTargetMaster = newTargetMaster;

            if (!ReferenceEquals(_currentTargetMaster, null))
            {
                QualityDuplicatorMinionInventoryController.OnOwnerDiscoveredGlobal += onDuplicatorMinionInventoryOwnerDiscoveredGlobal;
                QualityDuplicatorMinionInventoryController.OnOwnerLostGlobal += onDuplicatorMinionInventoryOwnerLostGlobal;
            }
            
            setCurrentMinionInventoryController(QualityDuplicatorMinionInventoryController.FindMinionInventoryController(_currentTargetMaster));
        }

        private void onDuplicatorMinionInventoryOwnerDiscoveredGlobal(QualityDuplicatorMinionInventoryController minionInventoryController)
        {
            if (ReferenceEquals(minionInventoryController.OwnerMaster, _currentTargetMaster))
            {
                setCurrentMinionInventoryController(minionInventoryController);
            }
        }

        private void onDuplicatorMinionInventoryOwnerLostGlobal(QualityDuplicatorMinionInventoryController minionInventoryController)
        {
            if (ReferenceEquals(minionInventoryController.OwnerMaster, _currentTargetMaster))
            {
                setCurrentMinionInventoryController(null);
            }
        }

        void setCurrentMinionInventoryController(QualityDuplicatorMinionInventoryController duplicatorAttachment)
        {
            if (ReferenceEquals(_currentTargetMinionInventoryController, duplicatorAttachment))
                return;

            _currentTargetMinionInventoryController = duplicatorAttachment;

            Inventory duplicatorInventory = _currentTargetMinionInventoryController ? _currentTargetMinionInventoryController.MinionMirrorInventory : null;

            if (_itemShareInventoryDisplayRoot)
            {
                _itemShareInventoryDisplayRoot.SetActive(duplicatorInventory != null);
            }

            if (_itemShareInventoryDisplay)
            {
                _itemShareInventoryDisplay.SetSubscribedInventory(duplicatorInventory);
            }
        }
    }
}
