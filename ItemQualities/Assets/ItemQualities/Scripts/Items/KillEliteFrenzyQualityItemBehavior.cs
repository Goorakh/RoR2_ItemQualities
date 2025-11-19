using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public class KillEliteFrenzyQualityItemBehavior : MonoBehaviour
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
                ItemQualitiesContent.BuffQualityGroups.KillEliteFrenzyBuff.EnsureBuffQualities(_body, QualityTier.None);
            }
        }

        void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                if (!_body.HasBuff(RoR2Content.Buffs.NoCooldowns) &&
                    ItemQualitiesContent.BuffQualityGroups.KillEliteFrenzyBuff.GetBuffCounts(_body).TotalQualityCount > 0)
                {
                    ItemQualitiesContent.BuffQualityGroups.KillEliteFrenzyBuff.EnsureBuffQualities(_body, QualityTier.None);
                }
            }
        }

        void onInventoryChanged()
        {
            QualityTier buffQualityTier = ItemQualitiesContent.ItemQualityGroups.KillEliteFrenzy.GetItemCountsEffective(_body.inventory).HighestQuality;
            ItemQualitiesContent.BuffQualityGroups.KillEliteFrenzyBuff.EnsureBuffQualities(_body, buffQualityTier);
        }
    }
}
