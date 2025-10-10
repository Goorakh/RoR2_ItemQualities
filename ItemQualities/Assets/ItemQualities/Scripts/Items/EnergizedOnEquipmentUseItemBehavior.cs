using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public class EnergizedOnEquipmentUseItemBehavior : MonoBehaviour
    {
        CharacterBody _body;

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
        }

        void OnEnable()
        {
            if (NetworkServer.active)
            {
                _body.onInventoryChanged += onInventoryChanged;
            }
        }

        void OnDisable()
        {
            _body.onInventoryChanged -= onInventoryChanged;
            ItemQualitiesContent.BuffQualityGroups.Energized.EnsureBuffQualities(_body, QualityTier.None, true);
        }

        void onInventoryChanged()
        {
            QualityTier buffQualityTier = ItemQualitiesContent.ItemQualityGroups.EnergizedOnEquipmentUse.GetHighestQualityInInventory(_body.inventory);
            ItemQualitiesContent.BuffQualityGroups.Energized.EnsureBuffQualities(_body, buffQualityTier, true);
        }
    }
}
