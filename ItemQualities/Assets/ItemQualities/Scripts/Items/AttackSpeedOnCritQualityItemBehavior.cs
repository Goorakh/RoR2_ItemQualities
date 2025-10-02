using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public class AttackSpeedOnCritQualityItemBehavior : MonoBehaviour
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
                ItemQualitiesContent.BuffQualityGroups.AttackSpeedOnCrit.EnsureBuffQualities(_body, QualityTier.None);
            }
        }

        void onInventoryChanged()
        {
            QualityTier buffQualityTier = ItemQualitiesContent.ItemQualityGroups.AttackSpeedOnCrit.GetHighestQualityInInventory(_body.inventory);
            ItemQualitiesContent.BuffQualityGroups.AttackSpeedOnCrit.EnsureBuffQualities(_body, buffQualityTier);
        }
    }
}
