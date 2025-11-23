using RoR2;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public class BoostAllStatsQualityItemBehavior : MonoBehaviour
    {
        CharacterBody _body;

        float _buffCheckTimer = 0f;

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
        }

        void OnEnable()
        {
            _buffCheckTimer = 0f;

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
                setBuffActive(false);
            }
        }

        void onInventoryChanged()
        {
            QualityTier buffQualityTier = ItemQualitiesContent.ItemQualityGroups.BoostAllStats.GetItemCountsEffective(_body.inventory).HighestQuality;
            ItemQualitiesContent.BuffQualityGroups.BoostAllStatsBuff.EnsureBuffQualities(_body, buffQualityTier);
        }

        void FixedUpdate()
        {
            if (!NetworkServer.active)
                return;

            _buffCheckTimer -= Time.fixedDeltaTime;
            if (_buffCheckTimer <= 0f)
            {
                _buffCheckTimer = 0.2f;

                int growthNectarBuffCount = 0;
                foreach (BuffIndex buffIndex in BuffCatalog.nonHiddenBuffIndices)
                {
                    if (_body.HasBuff(buffIndex) &&
                        buffIndex != DLC2Content.Buffs.BoostAllStatsBuff.buffIndex &&
                        !BuffCatalog.ignoreGrowthNectarIndices.Contains(buffIndex))
                    {
                        growthNectarBuffCount++;
                    }
                }

                setBuffActive(growthNectarBuffCount >= 8);
            }
        }

        void setBuffActive(bool active)
        {
            if (active != ItemQualitiesContent.BuffQualityGroups.BoostAllStatsBuff.HasQualityBuff(_body))
            {
                if (active)
                {
                    QualityTier buffQualityTier = ItemQualitiesContent.ItemQualityGroups.BoostAllStats.GetItemCountsEffective(_body.inventory).HighestQuality;
                    _body.AddBuff(ItemQualitiesContent.BuffQualityGroups.BoostAllStatsBuff.GetBuffIndex(buffQualityTier));
                }
                else
                {
                    ItemQualitiesContent.BuffQualityGroups.BoostAllStatsBuff.EnsureBuffQualities(_body, QualityTier.None);
                }
            }
        }
    }
}
