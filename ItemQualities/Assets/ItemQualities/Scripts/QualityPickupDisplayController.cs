using RoR2;
using UnityEngine;
using UnityEngine.UI;

namespace ItemQualities
{
    public class QualityPickupDisplayController : MonoBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.PickupDisplay.Start += PickupDisplay_Start;
        }

        static void PickupDisplay_Start(On.RoR2.PickupDisplay.orig_Start orig, PickupDisplay self)
        {
            orig(self);
            GameObject qualityDisplay = Instantiate(ItemQualitiesContent.Prefabs.QualityPickupDisplay, self.transform);
            QualityPickupDisplayController qualityDisplayController = qualityDisplay.GetComponent<QualityPickupDisplayController>();
            qualityDisplayController._pickupDisplay = self;
        }

        public Image QualityIcon;

        public GameObject QualityItemEffect;

        PickupDisplay _pickupDisplay;

        PickupIndex _lastPickupIndex = PickupIndex.none;

        void Awake()
        {
            if (!_pickupDisplay)
            {
                _pickupDisplay = GetComponentInParent<PickupDisplay>();
            }
        }

        void OnEnable()
        {
            refreshQualityIcon();
        }

        void FixedUpdate()
        {
            PickupIndex currentPickupIndex = _pickupDisplay ? _pickupDisplay.pickupIndex : PickupIndex.none;
            if (_lastPickupIndex != currentPickupIndex)
            {
                refreshQualityIcon();
            }
        }

        void refreshQualityIcon()
        {
            PickupIndex currentPickupIndex = _pickupDisplay ? _pickupDisplay.pickupIndex : PickupIndex.none;
            PickupDef currentPickup = PickupCatalog.GetPickupDef(currentPickupIndex);

            QualityTier qualityTier = QualityCatalog.GetQualityTier(currentPickupIndex);
            QualityTierDef qualityTierDef = QualityCatalog.GetQualityTierDef(qualityTier);

            Sprite qualityIcon = null;
            if (qualityTierDef)
            {
                qualityIcon = qualityTierDef.icon;

                bool isConsumed = false;
                if (currentPickup != null)
                {
                    ItemDef itemDef = ItemCatalog.GetItemDef(currentPickup.itemIndex);
                    if (itemDef && itemDef.isConsumed)
                    {
                        isConsumed = true;
                    }

                    // Intentionally ignoring equipments, since unlike items, they're still usable when consumed
                }

                if (isConsumed && qualityTierDef.consumedIcon)
                {
                    qualityIcon = qualityTierDef.consumedIcon;
                }
            }

            QualityIcon.sprite = qualityIcon;

            if (QualityItemEffect)
            {
                QualityItemEffect.SetActive(qualityTier > QualityTier.None);
            }

            _lastPickupIndex = currentPickupIndex;
        }
    }
}
