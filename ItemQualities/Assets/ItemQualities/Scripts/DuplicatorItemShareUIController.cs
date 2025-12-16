using ItemQualities.Items;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2.UI;
using RoR2BepInExPack.GameAssetPaths.Version_1_35_0;
using UnityEngine;

namespace ItemQualities
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

        CharacterBody _currentTargetBody;
        GameObject _currentTargetBodyObject;
        DuplicatorQualityAttachmentBehavior _currentBodyDuplicatorAttachment;

        void Awake()
        {
            _hud = GetComponent<HUD>();
            _childLocator = GetComponent<ChildLocator>();
        }

        void Start()
        {
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

            Inventory duplicatorInventory = _currentBodyDuplicatorAttachment ? _currentBodyDuplicatorAttachment.MinionMirrorInventory : null;
            setDisplayedDuplicatorInventory(duplicatorInventory);
        }

        void OnEnable()
        {
            setTargetBodyObject(_hud.targetBodyObject);

            HUD.onHudTargetChangedGlobal += onHudTargetChangedGlobal;
            DuplicatorQualityAttachmentBehavior.OnAttachedBodyChangedGlobal += onDuplicatorAttachmentBodyChangedGlobal;
        }

        void OnDisable()
        {
            DuplicatorQualityAttachmentBehavior.OnAttachedBodyChangedGlobal -= onDuplicatorAttachmentBodyChangedGlobal;
            HUD.onHudTargetChangedGlobal -= onHudTargetChangedGlobal;

            setTargetBodyObject(null);
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
            
            DuplicatorQualityAttachmentBehavior currentBodyDuplicatorAttachment = null;
            if (_currentTargetBody)
            {
                foreach (DuplicatorQualityAttachmentBehavior duplicatorAttachment in InstanceTracker.GetInstancesList<DuplicatorQualityAttachmentBehavior>())
                {
                    if (duplicatorAttachment.AttachedBody == _currentTargetBody)
                    {
                        currentBodyDuplicatorAttachment = duplicatorAttachment;
                        break;
                    }
                }
            }

            setCurrentBodyDuplicatorAttachment(currentBodyDuplicatorAttachment);
        }

        void onDuplicatorAttachmentBodyChangedGlobal(DuplicatorQualityAttachmentBehavior duplicatorAttachment)
        {
            if (_currentTargetBody && duplicatorAttachment.AttachedBody == _currentTargetBody)
            {
                setCurrentBodyDuplicatorAttachment(duplicatorAttachment);
            }
            else if (duplicatorAttachment == _currentBodyDuplicatorAttachment)
            {
                setCurrentBodyDuplicatorAttachment(null);
            }
        }

        void setCurrentBodyDuplicatorAttachment(DuplicatorQualityAttachmentBehavior duplicatorAttachment)
        {
            if (_currentBodyDuplicatorAttachment == duplicatorAttachment)
                return;

            _currentBodyDuplicatorAttachment = duplicatorAttachment;

            Inventory duplicatorInventory = _currentBodyDuplicatorAttachment ? _currentBodyDuplicatorAttachment.MinionMirrorInventory : null;
            setDisplayedDuplicatorInventory(duplicatorInventory);
        }

        void setDisplayedDuplicatorInventory(Inventory duplicatorInventory)
        {
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
