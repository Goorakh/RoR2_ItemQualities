using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public class ArmorPlateQualityItemBehavior : MonoBehaviour
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

            if (NetworkServer.active)
            {
                ItemQualitiesContent.BuffQualityGroups.ArmorPlateBuildup.EnsureBuffQualities(_body, QualityTier.None);
            }
        }

        void onInventoryChanged()
        {
            QualityTier buffQualityTier = ItemQualitiesContent.ItemQualityGroups.ArmorPlate.GetItemCountsEffective(_body.inventory).HighestQuality;
            ItemQualitiesContent.BuffQualityGroups.ArmorPlateBuildup.EnsureBuffQualities(_body, buffQualityTier);

            if (buffQualityTier > QualityTier.None)
            {
                ItemQualitiesContent.BuffQualityGroups.ArmorPlateBuff.EnsureBuffQualities(_body, buffQualityTier);
            }
        }
    }
}
