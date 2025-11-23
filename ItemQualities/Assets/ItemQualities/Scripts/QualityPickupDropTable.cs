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

        [Tooltip("If set, all pickups added to the droptable must have all quality tiers implemented, regardless of the quality tier weights")]
        public bool RequireAllQualitiesImplemented;

        public float BaseQualityWeight = 0f;

        public float UncommonQualityWeight = 0.7f;

        public float RareQualityWeight = 0.2f;

        public float EpicQualityWeight = 0.08f;

        public float LegendaryQualityWeight = 0.02f;

        readonly WeightedSelection<UniquePickup> _selector = new WeightedSelection<UniquePickup>();

        readonly WeightedSelection<QualityTier> _qualityTierSelection = new WeightedSelection<QualityTier>();

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
            return RequiredItemTags.Length > 0 || BannedItemTags.Length > 0 || RequireAllQualitiesImplemented;
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

                if (RequireAllQualitiesImplemented)
                {
                    ItemQualityGroup itemQualityGroup = QualityCatalog.GetItemQualityGroup(QualityCatalog.FindItemQualityGroupIndex(pickupDef.itemIndex));
                    if (!itemQualityGroup)
                        return false;

                    for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        if (itemQualityGroup.GetItemIndex(qualityTier) == ItemIndex.None)
                        {
                            return false;
                        }
                    }
                }
            }
            else if (pickupDef.equipmentIndex != EquipmentIndex.None)
            {
                if (RequireAllQualitiesImplemented)
                {
                    EquipmentQualityGroup equipmentQualityGroup = QualityCatalog.GetEquipmentQualityGroup(QualityCatalog.FindEquipmentQualityGroupIndex(pickupDef.equipmentIndex));
                    if (!equipmentQualityGroup)
                        return false;

                    for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                    {
                        if (equipmentQualityGroup.GetEquipmentIndex(qualityTier) == EquipmentIndex.None)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        void generateWeightedSelection(Run run)
        {
            _selector.Clear();
            addPickups(run.availableTier1DropList, Tier1Weight);
            addPickups(run.availableTier2DropList, Tier2Weight);
            addPickups(run.availableTier3DropList, Tier3Weight);
            addPickups(run.availableBossDropList, BossWeight);
            addPickups(run.availableLunarItemDropList, LunarItemWeight);
            addPickups(run.availableLunarEquipmentDropList, LunarEquipmentWeight);
            addPickups(run.availableLunarCombinedDropList, LunarCombinedWeight);
            addPickups(run.availableEquipmentDropList, EquipmentWeight);
            addPickups(run.availableVoidTier1DropList, VoidTier1Weight);
            addPickups(run.availableVoidTier2DropList, VoidTier2Weight);
            addPickups(run.availableVoidTier3DropList, VoidTier3Weight);
            addPickups(run.availableVoidBossDropList, VoidBossWeight);

            _qualityTierSelection.Clear();
            addQuality(QualityTier.None, BaseQualityWeight);
            addQuality(QualityTier.Uncommon, UncommonQualityWeight);
            addQuality(QualityTier.Rare, RareQualityWeight);
            addQuality(QualityTier.Epic, EpicQualityWeight);
            addQuality(QualityTier.Legendary, LegendaryQualityWeight);

            void addPickups(List<PickupIndex> sourceDropList, float weight)
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
                                    _selector.AddChoice(new UniquePickup(qualityPickupIndex), weight * qualityWeight);
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

            void addQuality(QualityTier qualityTier, float weight)
            {
                if (weight <= 0f)
                    return;

                _qualityTierSelection.AddChoice(qualityTier, weight);
            }
        }

        QualityTier rollQuality(Xoroshiro128Plus rng)
        {
            return _qualityTierSelection.Evaluate(rng.nextNormalizedFloat);
        }

        PickupIndex tryRerollQuality(PickupIndex pickupIndex, Xoroshiro128Plus rng, int qualityLuck)
        {
            QualityTier currentPickupQualityTier = QualityCatalog.GetQualityTier(pickupIndex);

            for (int i = 0; i < qualityLuck; i++)
            {
                QualityTier qualityTier = rollQuality(rng);
                PickupIndex qualityPickupIndexCandidate = QualityCatalog.GetPickupIndexOfQuality(pickupIndex, qualityTier);
                if (qualityTier > currentPickupQualityTier && (!IsFilterRequired() || PassesFilter(qualityPickupIndexCandidate)))
                {
                    pickupIndex = qualityPickupIndexCandidate;
                    currentPickupQualityTier = qualityTier;
                }
            }

            return pickupIndex;
        }

        public override UniquePickup GeneratePickupPreReplacement(Xoroshiro128Plus rng)
        {
            rng = new Xoroshiro128Plus(rng.nextUlong);

            UniquePickup pickup = GeneratePickupFromWeightedSelection(rng, _selector);

            PickupRollInfo rollInfo = DropTableQualityHandler.GetCurrentPickupRollInfo();
            pickup = pickup.WithPickupIndex(tryRerollQuality(pickup.pickupIndex, rng, rollInfo.Luck));

            return pickup;
        }

        public override void GenerateDistinctPickupsPreReplacement(List<UniquePickup> dest, int desiredCount, Xoroshiro128Plus rng)
        {
            rng = new Xoroshiro128Plus(rng.nextUlong);

            GenerateDistinctFromWeightedSelection(dest, desiredCount, rng, _selector);

            PickupRollInfo rollInfo = DropTableQualityHandler.GetCurrentPickupRollInfo();
            for (int i = 0; i < dest.Count; i++)
            {
                UniquePickup pickup = dest[i];

                bool changed = false;

                if (pickup.isValid)
                {
                    PickupIndex qualityPickupIndex = tryRerollQuality(pickup.pickupIndex, rng, rollInfo.Luck);
                    if (qualityPickupIndex != pickup.pickupIndex)
                    {
                        pickup = pickup.WithPickupIndex(qualityPickupIndex);
                        changed = true;
                    }
                }

                if (changed)
                {
                    dest[i] = pickup;
                }
            }
        }

        public override int GetPickupCount()
        {
            return _selector.Count;
        }
    }
}
