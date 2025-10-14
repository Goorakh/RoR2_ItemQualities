using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemQualities
{
    [CreateAssetMenu(menuName = "ItemQualities/DropTable/QualityPickupDropTable")]
    public class QualityPickupDropTable : PickupDropTable
    {
        [Header("Pickups")]

        public ItemTag[] RequiredItemTags = Array.Empty<ItemTag>();

        public ItemTag[] BannedItemTags = Array.Empty<ItemTag>();

        public float Tier1Weight = 0.8f;

        public float Tier2Weight = 0.2f;

        public float Tier3Weight = 0.01f;

        public float BossWeight;

        public float LunarEquipmentWeight;

        public float LunarItemWeight;

        public float LunarCombinedWeight;

        public float EquipmentWeight;

        public float VoidTier1Weight;

        public float VoidTier2Weight;

        public float VoidTier3Weight;

        public float VoidBossWeight;

        [Header("Quality")]

        public float BaseQualityWeight = 1f;

        public float UncommonQualityWeight = 0f;

        public float RareQualityWeight = 0f;

        public float EpicQualityWeight = 0f;

        public float LegendaryQualityWeight = 0f;

        readonly WeightedSelection<PickupIndex> _selector = new WeightedSelection<PickupIndex>(8);

        public override void Regenerate(Run run)
        {
            generateWeightedSelection(run);
        }

        public void RegenerateDropTable(Run run)
        {
            generateWeightedSelection(run);
        }

        public bool IsFilterRequired()
        {
            return RequiredItemTags.Length > 0 || BannedItemTags.Length > 0;
        }

        public bool PassesFilter(PickupIndex pickupIndex)
        {
            PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
            if (pickupDef.itemIndex != ItemIndex.None)
            {
                ItemDef itemDef = ItemCatalog.GetItemDef(pickupDef.itemIndex);

                foreach (ItemTag requiredTag in RequiredItemTags)
                {
                    if (Array.IndexOf(itemDef.tags, requiredTag) == -1)
                    {
                        return false;
                    }
                }

                foreach (ItemTag bannedTag in BannedItemTags)
                {
                    if (Array.IndexOf(itemDef.tags, bannedTag) != -1)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        void generateWeightedSelection(Run run)
        {
            void add(List<PickupIndex> sourceDropList, float weight)
            {
                if (weight <= 0f || sourceDropList.Count == 0)
                    return;

                foreach (PickupIndex pickupIndex in sourceDropList)
                {
                    if ((!IsFilterRequired() || PassesFilter(pickupIndex)) && QualityCatalog.GetQualityTier(pickupIndex) == QualityTier.None)
                    {
                        void tryAddQualityChoice(QualityTier qualityTier, float qualityWeight)
                        {
                            if (qualityWeight > 0f)
                            {
                                PickupIndex qualityPickupIndex = QualityCatalog.GetPickupIndexOfQuality(pickupIndex, qualityTier);
                                if (qualityPickupIndex != PickupIndex.none && (qualityTier == QualityTier.None || qualityPickupIndex != pickupIndex))
                                {
                                    _selector.AddChoice(qualityPickupIndex, weight * qualityWeight);
                                }
                            }
                        }

                        tryAddQualityChoice(QualityTier.None, BaseQualityWeight);
                        tryAddQualityChoice(QualityTier.Uncommon, UncommonQualityWeight);
                        tryAddQualityChoice(QualityTier.Rare, RareQualityWeight);
                        tryAddQualityChoice(QualityTier.Epic, EpicQualityWeight);
                        tryAddQualityChoice(QualityTier.Legendary, LegendaryQualityWeight);
                    }
                }
            }

            _selector.Clear();
            add(run.availableTier1DropList, Tier1Weight);
            add(run.availableTier2DropList, Tier2Weight);
            add(run.availableTier3DropList, Tier3Weight);
            add(run.availableBossDropList, BossWeight);
            add(run.availableLunarItemDropList, LunarItemWeight);
            add(run.availableLunarEquipmentDropList, LunarEquipmentWeight);
            add(run.availableLunarCombinedDropList, LunarCombinedWeight);
            add(run.availableEquipmentDropList, EquipmentWeight);
            add(run.availableVoidTier1DropList, VoidTier1Weight);
            add(run.availableVoidTier2DropList, VoidTier2Weight);
            add(run.availableVoidTier3DropList, VoidTier3Weight);
            add(run.availableVoidBossDropList, VoidBossWeight);
        }

        public override PickupIndex GenerateDropPreReplacement(Xoroshiro128Plus rng)
        {
            return GenerateDropFromWeightedSelection(rng, _selector);
        }

        public override int GetPickupCount()
        {
            return _selector.Count;
        }

        public override PickupIndex[] GenerateUniqueDropsPreReplacement(int maxDrops, Xoroshiro128Plus rng)
        {
            return GenerateUniqueDropsFromWeightedSelection(maxDrops, rng, _selector);
        }
    }
}
