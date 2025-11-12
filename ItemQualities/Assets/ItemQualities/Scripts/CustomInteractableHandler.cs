using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Diagnostics;
using UnityEngine;

namespace ItemQualities
{
    static class CustomInteractableHandler
    {
        [SystemInitializer(typeof(CustomCostTypeIndex))]
        static void Init()
        {
            On.RoR2.ClassicStageInfo.RebuildCards += ClassicStageInfo_RebuildCards;

            AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Chest1StealthedVariant.Chest1StealthedVariant_prefab).OnSuccess(cloakedChest =>
            {
                if (cloakedChest.TryGetComponent(out ChestBehavior cloakedChestBehavior))
                {
                    PickupDropTable qualityDropTable = null;
                    if (ItemQualitiesContent.SpawnCards.QualityChest1 &&
                        ItemQualitiesContent.SpawnCards.QualityChest1.prefab &&
                        ItemQualitiesContent.SpawnCards.QualityChest1.prefab.TryGetComponent(out ChestBehavior qualityChestBehavior))
                    {
                        qualityDropTable = qualityChestBehavior.dropTable;
                    }

                    if (qualityDropTable)
                    {
                        cloakedChestBehavior.dropTable = qualityDropTable;
                    }
                }
            });

            static void setInteractableCostType(InteractableSpawnCard spawnCard, CostTypeIndex costType)
            {
                if (!spawnCard)
                {
                    Log.Error($"Null spawncard: {new StackTrace()}");
                    return;
                }

                if (!spawnCard.prefab)
                {
                    Log.Error($"Null prefab in spawncard {spawnCard}");
                    return;
                }

                if (!spawnCard.prefab.TryGetComponent(out PurchaseInteraction purchaseInteraction))
                {
                    Log.Error($"{spawnCard} prefab {spawnCard.prefab.name} is missing PurchaseInteraction component");
                    return;
                }

                purchaseInteraction.costType = costType;
            }

            setInteractableCostType(ItemQualitiesContent.SpawnCards.QualityDuplicator, CustomCostTypeIndex.WhiteItemQuality);
            setInteractableCostType(ItemQualitiesContent.SpawnCards.QualityDuplicatorLarge, CustomCostTypeIndex.GreenItemQuality);
            setInteractableCostType(ItemQualitiesContent.SpawnCards.QualityDuplicatorMilitary, CustomCostTypeIndex.RedItemQuality);
            setInteractableCostType(ItemQualitiesContent.SpawnCards.QualityDuplicatorWild, CustomCostTypeIndex.BossItemQuality);
        }

        static void ClassicStageInfo_RebuildCards(On.RoR2.ClassicStageInfo.orig_RebuildCards orig, ClassicStageInfo self, DirectorCardCategorySelection forcedMonsterCategory, DirectorCardCategorySelection forcedInteractableCategory)
        {
            orig(self, forcedMonsterCategory, forcedInteractableCategory);
            tryAddCustomInteractables(self.interactableCategories);
        }

        static void tryAddCustomInteractables(DirectorCardCategorySelection dccs)
        {
            bool addedQualityEquipmentBarrel = false;
            bool addedQualityChest1 = false;
            bool addedQualityChest2 = false;
            bool addedQualityDuplicator = false;
            bool addedQualityDuplicatorLarge = false;
            bool addedQualityDuplicatorMilitary = false;
            bool addedQualityDuplicatorWild = false;

            foreach (DirectorCardCategorySelection.Category category in dccs.categories)
            {
                foreach (DirectorCard card in category.cards)
                {
                    addedQualityEquipmentBarrel |= card.spawnCard == ItemQualitiesContent.SpawnCards.QualityEquipmentBarrel;
                    addedQualityChest1 |= card.spawnCard == ItemQualitiesContent.SpawnCards.QualityChest1;
                    addedQualityChest2 |= card.spawnCard == ItemQualitiesContent.SpawnCards.QualityChest2;
                    addedQualityDuplicator |= card.spawnCard == ItemQualitiesContent.SpawnCards.QualityDuplicator;
                    addedQualityDuplicatorLarge |= card.spawnCard == ItemQualitiesContent.SpawnCards.QualityDuplicatorLarge;
                    addedQualityDuplicatorMilitary |= card.spawnCard == ItemQualitiesContent.SpawnCards.QualityDuplicatorMilitary;
                    addedQualityDuplicatorWild |= card.spawnCard == ItemQualitiesContent.SpawnCards.QualityDuplicatorWild;
                }
            }

            for (int i = 0; i < dccs.categories.Length; i++)
            {
                ref DirectorCardCategorySelection.Category category = ref dccs.categories[i];

                DirectorCard equipmentBarrelCard = null;

                DirectorCard categoryChest1Card = null;

                DirectorCard categoryChest2Card = null;

                DirectorCard duplicatorCard = null;

                DirectorCard duplicatorLargeCard = null;

                DirectorCard duplicatorMilitaryCard = null;

                DirectorCard duplicatorWildCard = null;

                foreach (DirectorCard card in category.cards)
                {
                    if (equipmentBarrelCard == null &&
                        matchDirectorCard(card, "iscEquipmentBarrel", RoR2_Base_EquipmentBarrel.iscEquipmentBarrel_asset))
                    {
                        equipmentBarrelCard = card;
                    }

                    if (categoryChest1Card == null &&
                        (matchDirectorCard(card, "iscCategoryChestDamage", RoR2_Base_CategoryChest.iscCategoryChestDamage_asset) ||
                         matchDirectorCard(card, "iscCategoryChestUtility", RoR2_Base_CategoryChest.iscCategoryChestUtility_asset) ||
                         matchDirectorCard(card, "iscCategoryChestHealing", RoR2_Base_CategoryChest.iscCategoryChestHealing_asset)))
                    {
                        categoryChest1Card = card;
                    }

                    if (categoryChest2Card == null &&
                        (matchDirectorCard(card, "iscCategoryChest2Damage", RoR2_DLC1_CategoryChest2.iscCategoryChest2Damage_asset) ||
                         matchDirectorCard(card, "iscCategoryChest2Utility", RoR2_DLC1_CategoryChest2.iscCategoryChest2Utility_asset) ||
                         matchDirectorCard(card, "iscCategoryChest2Healing", RoR2_DLC1_CategoryChest2.iscCategoryChest2Healing_asset)))
                    {
                        categoryChest2Card = card;
                    }

                    if (duplicatorCard == null &&
                        matchDirectorCard(card, "iscDuplicator", RoR2_Base_Duplicator.iscDuplicator_asset))
                    {
                        duplicatorCard = card;
                    }

                    if (duplicatorLargeCard == null &&
                        matchDirectorCard(card, "iscDuplicatorLarge", RoR2_Base_DuplicatorLarge.iscDuplicatorLarge_asset))
                    {
                        duplicatorLargeCard = card;
                    }

                    if (duplicatorMilitaryCard == null &&
                        matchDirectorCard(card, "iscDuplicatorMilitary", RoR2_Base_DuplicatorMilitary.iscDuplicatorMilitary_asset))
                    {
                        duplicatorMilitaryCard = card;
                    }

                    if (duplicatorWildCard == null &&
                        matchDirectorCard(card, "iscDuplicatorWild", RoR2_Base_DuplicatorWild.iscDuplicatorWild_asset))
                    {
                        duplicatorWildCard = card;
                    }
                }

                if (equipmentBarrelCard != null && !addedQualityEquipmentBarrel)
                {
                    DirectorCard qualityEquipmentBarrelCard = new DirectorCard
                    {
                        spawnCard = ItemQualitiesContent.SpawnCards.QualityEquipmentBarrel,
                        selectionWeight = Mathf.Max(1, Mathf.RoundToInt(equipmentBarrelCard.selectionWeight * 0.35f)),
                        minimumStageCompletions = equipmentBarrelCard.minimumStageCompletions
                    };

                    ArrayUtils.ArrayAppend(ref category.cards, qualityEquipmentBarrelCard);

                    Log.Debug($"Appended quality equipment barrel to {dccs.name} ({category.name}) with weight {qualityEquipmentBarrelCard.selectionWeight}");

                    addedQualityEquipmentBarrel = true;
                }

                if (categoryChest1Card != null && !addedQualityChest1)
                {
                    DirectorCard qualityChest1Card = new DirectorCard
                    {
                        spawnCard = ItemQualitiesContent.SpawnCards.QualityChest1,
                        selectionWeight = Mathf.Max(1, Mathf.RoundToInt(categoryChest1Card.selectionWeight * 2f)),
                        minimumStageCompletions = categoryChest1Card.minimumStageCompletions
                    };

                    ArrayUtils.ArrayAppend(ref category.cards, qualityChest1Card);

                    Log.Debug($"Appended quality small chest to {dccs.name} ({category.name}) with weight {qualityChest1Card.selectionWeight}");

                    addedQualityChest1 = true;
                }

                if (categoryChest2Card != null && !addedQualityChest2)
                {
                    DirectorCard qualityChest2Card = new DirectorCard
                    {
                        spawnCard = ItemQualitiesContent.SpawnCards.QualityChest2,
                        selectionWeight = Mathf.Max(1, Mathf.RoundToInt(categoryChest2Card.selectionWeight * 1.5f)),
                        minimumStageCompletions = categoryChest2Card.minimumStageCompletions
                    };

                    ArrayUtils.ArrayAppend(ref category.cards, qualityChest2Card);

                    Log.Debug($"Appended quality large chest to {dccs.name} ({category.name}) with weight {qualityChest2Card.selectionWeight}");

                    addedQualityChest2 = true;
                }

                if (duplicatorCard != null && !addedQualityDuplicator)
                {
                    DirectorCard qualityDuplicatorCard = new DirectorCard
                    {
                        spawnCard = ItemQualitiesContent.SpawnCards.QualityDuplicator,
                        selectionWeight = Mathf.Max(1, Mathf.RoundToInt(duplicatorCard.selectionWeight * 0.35f)),
                        minimumStageCompletions = duplicatorCard.minimumStageCompletions
                    };

                    ArrayUtils.ArrayAppend(ref category.cards, qualityDuplicatorCard);

                    Log.Debug($"Appended quality white printer to {dccs.name} ({category.name}) with weight {qualityDuplicatorCard.selectionWeight}");

                    addedQualityDuplicator = true;
                }

                if (duplicatorLargeCard != null && !addedQualityDuplicatorLarge)
                {
                    DirectorCard qualityDuplicatorLargeCard = new DirectorCard
                    {
                        spawnCard = ItemQualitiesContent.SpawnCards.QualityDuplicatorLarge,
                        selectionWeight = Mathf.Max(1, Mathf.RoundToInt(duplicatorLargeCard.selectionWeight * 0.35f)),
                        minimumStageCompletions = duplicatorLargeCard.minimumStageCompletions
                    };

                    ArrayUtils.ArrayAppend(ref category.cards, qualityDuplicatorLargeCard);

                    Log.Debug($"Appended quality green printer to {dccs.name} ({category.name}) with weight {qualityDuplicatorLargeCard.selectionWeight}");

                    addedQualityDuplicatorLarge = true;
                }

                if (duplicatorMilitaryCard != null && !addedQualityDuplicatorMilitary)
                {
                    DirectorCard qualityDuplicatorMilitaryCard = new DirectorCard
                    {
                        spawnCard = ItemQualitiesContent.SpawnCards.QualityDuplicatorMilitary,
                        selectionWeight = Mathf.Max(1, Mathf.RoundToInt(duplicatorMilitaryCard.selectionWeight * 0.35f)),
                        minimumStageCompletions = duplicatorMilitaryCard.minimumStageCompletions
                    };

                    ArrayUtils.ArrayAppend(ref category.cards, qualityDuplicatorMilitaryCard);

                    Log.Debug($"Appended quality red printer to {dccs.name} ({category.name}) with weight {qualityDuplicatorMilitaryCard.selectionWeight}");

                    addedQualityDuplicatorMilitary = true;
                }

                if (duplicatorWildCard != null && !addedQualityDuplicatorWild)
                {
                    DirectorCard qualityDuplicatorWildCard = new DirectorCard
                    {
                        spawnCard = ItemQualitiesContent.SpawnCards.QualityDuplicatorWild,
                        selectionWeight = Mathf.Max(1, Mathf.RoundToInt(duplicatorWildCard.selectionWeight * 0.35f)),
                        minimumStageCompletions = duplicatorWildCard.minimumStageCompletions
                    };

                    ArrayUtils.ArrayAppend(ref category.cards, qualityDuplicatorWildCard);

                    Log.Debug($"Appended quality boss printer to {dccs.name} ({category.name}) with weight {qualityDuplicatorWildCard.selectionWeight}");

                    addedQualityDuplicatorWild = true;
                }
            }
        }

        static bool matchDirectorCard(DirectorCard directorCard, string spawnCardName, string spawnCardGuid)
        {
            if (directorCard == null)
                return false;

            return (directorCard.spawnCard && directorCard.spawnCard.name == spawnCardName) ||
                   (directorCard.spawnCardReference != null && directorCard.spawnCardReference.AssetGUID == spawnCardGuid);
        }
    }
}
