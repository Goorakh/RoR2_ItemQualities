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
            QualityTier buffQualityTier = ItemQualitiesContent.ItemQualityGroups.EnergizedOnEquipmentUse.GetItemCountsEffective(_body.inventory).HighestQuality;
            ItemQualitiesContent.BuffQualityGroups.Energized.EnsureBuffQualities(_body, buffQualityTier, true);
        }
    }
}
