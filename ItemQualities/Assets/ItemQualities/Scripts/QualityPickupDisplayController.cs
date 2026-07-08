using RoR2;
using System;
using UnityEngine;

namespace ItemQualities
{
    public sealed class QualityPickupDisplayController : MonoBehaviour
    {
        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.PickupDisplay.Start += PickupDisplay_Start;
            On.RoR2.PickupDisplay.RebuildModel += PickupDisplay_RebuildModel;
        }

        private static void PickupDisplay_Start(On.RoR2.PickupDisplay.orig_Start orig, PickupDisplay self)
        {
            orig(self);
            GameObject qualityDisplay = Instantiate(ItemQualitiesContent.Prefabs.QualityPickupDisplay, self.transform);
            QualityPickupDisplayController qualityDisplayController = qualityDisplay.GetComponent<QualityPickupDisplayController>();
            qualityDisplayController._pickupDisplay = self;
        }

        private static void PickupDisplay_RebuildModel(On.RoR2.PickupDisplay.orig_RebuildModel orig, PickupDisplay self, GameObject modelObjectOverride)
        {
            orig(self, modelObjectOverride);

            QualityPickupDisplayController qualityDisplayController = self.GetComponentInChildren<QualityPickupDisplayController>();
            if (qualityDisplayController)
            {
                self.modelRenderers.AddRange(qualityDisplayController.Renderers);
            }
        }

        public SpriteRenderer QualityIconRenderer;

        public GameObject QualityItemEffect;

        public Renderer[] Renderers = Array.Empty<Renderer>();

        private PickupDisplay _pickupDisplay;

        private PickupIndex _lastPickupIndex = PickupIndex.none;

        private void Awake()
        {
            if (!_pickupDisplay)
            {
                _pickupDisplay = GetComponentInParent<PickupDisplay>();
            }
        }

        private void OnEnable()
        {
            _pickupDisplay.modelRenderers?.AddRange(Renderers);

            refreshQualityIcon();
        }

        private void OnDisable()
        {
            _pickupDisplay.modelRenderers?.RemoveAll(r => Array.IndexOf(Renderers, r) != -1);
        }

        private void FixedUpdate()
        {
            PickupIndex currentPickupIndex = _pickupDisplay ? _pickupDisplay.pickupState.pickupIndex : PickupIndex.none;
            if (_lastPickupIndex != currentPickupIndex)
            {
                refreshQualityIcon();
            }
        }

        private void refreshQualityIcon()
        {
            PickupIndex currentPickupIndex = _pickupDisplay ? _pickupDisplay.pickupState.pickupIndex : PickupIndex.none;
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

            QualityIconRenderer.sprite = qualityIcon;

            if (QualityItemEffect)
            {
                QualityItemEffect.SetActive(qualityTier > QualityTier.None);
            }

            _lastPickupIndex = currentPickupIndex;
        }
    }
}
