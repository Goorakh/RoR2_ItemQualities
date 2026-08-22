using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    public sealed class BoostAllStatsQualityItemBehavior : QualityItemBodyBehavior
    {
        private static BuffIndex[] _validBuffIndices = Array.Empty<BuffIndex>();

        [SystemInitializer(typeof(BuffCatalog))]
        private static void Init()
        {
            List<BuffIndex> validBuffIncides = new List<BuffIndex>(BuffCatalog.buffCount);

            for (BuffIndex buffIndex = 0; (int)buffIndex < BuffCatalog.buffCount; buffIndex++)
            {
                if (buffIndex != DLC2Content.Buffs.BoostAllStatsBuff.buffIndex && !BuffCatalog.ignoreGrowthNectarIndices.Contains(buffIndex))
                {
                    validBuffIncides.Add(buffIndex);
                }
            }

            _validBuffIndices = validBuffIncides.ToArray();
            Array.Sort(_validBuffIndices);
        }

        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.BoostAllStats;
        }

        private float _buffCheckTimer = 0f;

        private void OnDisable()
        {
            if (NetworkServer.active)
            {
                setBuffActive(false);
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            Body.ConvertQualityBuffsToTier(ItemQualitiesContent.BuffQualityGroups.BoostAllStatsBuff, Stacks.HighestQuality);
        }

        private void FixedUpdate()
        {
            if (!NetworkServer.active)
                return;

            _buffCheckTimer -= Time.fixedDeltaTime;
            if (_buffCheckTimer <= 0f)
            {
                _buffCheckTimer = 0.2f;

                int growthNectarBuffCount = 0;
                foreach (BuffIndex buffIndex in _validBuffIndices)
                {
                    if (Body.HasBuffRaw(buffIndex))
                    {
                        growthNectarBuffCount++;
                    }
                }

                setBuffActive(growthNectarBuffCount >= 8);
            }
        }

        private void setBuffActive(bool active)
        {
            bool isActive = Body.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.BoostAllStatsBuff).TotalQualityCount > 0;
            if (active != isActive)
            {
                if (active)
                {
                    BuffIndex buffIndex = ItemQualitiesContent.BuffQualityGroups.BoostAllStatsBuff.GetBuffIndex(Stacks.HighestQuality);
                    Body.AddBuff(buffIndex);
                }
                else
                {
                    Body.RemoveAllQualityBuffs(ItemQualitiesContent.BuffQualityGroups.BoostAllStatsBuff);
                }
            }
        }
    }
}
