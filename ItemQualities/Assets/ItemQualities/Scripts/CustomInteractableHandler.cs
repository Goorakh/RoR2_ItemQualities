using HG;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;

namespace ItemQualities
{
    static class CustomInteractableHandler
    {
        [SystemInitializer]
        static void Init()
        {
            SceneDirector.onGenerateInteractableCardSelection += SceneDirector_onGenerateInteractableCardSelection;

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
        }

        static void SceneDirector_onGenerateInteractableCardSelection(SceneDirector sceneDirector, DirectorCardCategorySelection dccs)
        {
            bool addedQualityEquipmentBarrel = false;
            bool addedQualityChest1 = false;
            bool addedQualityChest2 = false;

            for (int i = 0; i < dccs.categories.Length; i++)
            {
                ref DirectorCardCategorySelection.Category category = ref dccs.categories[i];

                bool containsEquipmentBarrel = false;
                int equipmentBarrelWeight = 0;

                bool containsCategoryChest1 = false;
                int categoryChest1Weight = 0;

                bool containsCategoryChest2 = false;
                int categoryChest2Weight = 0;

                foreach (DirectorCard card in category.cards)
                {
                    if (!containsEquipmentBarrel &&
                        matchDirectorCard(card, "iscEquipmentBarrel", RoR2_Base_EquipmentBarrel.iscEquipmentBarrel_asset))
                    {
                        equipmentBarrelWeight = card.selectionWeight;
                        containsEquipmentBarrel = true;
                    }

                    if (!containsCategoryChest1 &&
                        (matchDirectorCard(card, "iscCategoryChestDamage", RoR2_Base_CategoryChest.iscCategoryChestDamage_asset) ||
                         matchDirectorCard(card, "iscCategoryChestUtility", RoR2_Base_CategoryChest.iscCategoryChestUtility_asset) ||
                         matchDirectorCard(card, "iscCategoryChestHealing", RoR2_Base_CategoryChest.iscCategoryChestHealing_asset)))
                    {
                        categoryChest1Weight = card.selectionWeight;
                        containsCategoryChest1 = true;
                    }

                    if (!containsCategoryChest2 &&
                        (matchDirectorCard(card, "iscCategoryChest2Damage", RoR2_DLC1_CategoryChest2.iscCategoryChest2Damage_asset) ||
                         matchDirectorCard(card, "iscCategoryChest2Utility", RoR2_DLC1_CategoryChest2.iscCategoryChest2Utility_asset) ||
                         matchDirectorCard(card, "iscCategoryChest2Healing", RoR2_DLC1_CategoryChest2.iscCategoryChest2Healing_asset)))
                    {
                        categoryChest2Weight = card.selectionWeight;
                        containsCategoryChest2 = true;
                    }
                }

                if (containsEquipmentBarrel && !addedQualityEquipmentBarrel)
                {
                    DirectorCard qualityEquipmentBarrelCard = new DirectorCard
                    {
                        spawnCard = ItemQualitiesContent.SpawnCards.QualityEquipmentBarrel,
                        selectionWeight = Mathf.Max(1, Mathf.RoundToInt(equipmentBarrelWeight * 0.35f))
                    };

                    ArrayUtils.ArrayAppend(ref category.cards, qualityEquipmentBarrelCard);

                    Log.Debug($"Appended quality equipment barrel to {dccs.name} ({category.name}) with weight {qualityEquipmentBarrelCard.selectionWeight}");

                    addedQualityEquipmentBarrel = true;
                }

                if (containsCategoryChest1 && !addedQualityChest1)
                {
                    DirectorCard qualityChest1Card = new DirectorCard
                    {
                        spawnCard = ItemQualitiesContent.SpawnCards.QualityChest1,
                        selectionWeight = Mathf.Max(1, Mathf.RoundToInt(categoryChest1Weight * 2f))
                    };

                    ArrayUtils.ArrayAppend(ref category.cards, qualityChest1Card);

                    Log.Debug($"Appended quality small chest to {dccs.name} ({category.name}) with weight {qualityChest1Card.selectionWeight}");

                    addedQualityChest1 = true;
                }

                if (containsCategoryChest2 && !addedQualityChest2)
                {
                    DirectorCard qualityChest2Card = new DirectorCard
                    {
                        spawnCard = ItemQualitiesContent.SpawnCards.QualityChest2,
                        selectionWeight = Mathf.Max(1, Mathf.RoundToInt(categoryChest2Weight * 1.5f))
                    };

                    ArrayUtils.ArrayAppend(ref category.cards, qualityChest2Card);

                    Log.Debug($"Appended quality large chest to {dccs.name} ({category.name}) with weight {qualityChest2Card.selectionWeight}");

                    addedQualityChest2 = true;
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

        static bool containsEquipmentBarrels(DirectorCardCategorySelection dccs)
        {
            foreach (DirectorCardCategorySelection.Category category in dccs.categories)
            {
                foreach (DirectorCard card in category.cards)
                {
                    if (matchDirectorCard(card, "iscEquipmentBarrel", RoR2_Base_EquipmentBarrel.iscEquipmentBarrel_asset))
                    {
                        Log.Debug($"Found equip barrel in {dccs}");
                        return true;
                    }
                }
            }

            Log.Debug($"Did not find equip barrel in {dccs}");

            return false;
        }

        static bool containsAnyCategoryChest(DirectorCardCategorySelection dccs)
        {
            foreach (DirectorCardCategorySelection.Category category in dccs.categories)
            {
                foreach (DirectorCard card in category.cards)
                {
                    if (matchDirectorCard(card, "iscCategoryChestDamage", RoR2_Base_CategoryChest.iscCategoryChestDamage_asset) ||
                        matchDirectorCard(card, "iscCategoryChest2Damage", RoR2_DLC1_CategoryChest2.iscCategoryChest2Damage_asset) ||
                        matchDirectorCard(card, "iscCategoryChestUtility", RoR2_Base_CategoryChest.iscCategoryChestUtility_asset) ||
                        matchDirectorCard(card, "iscCategoryChest2Utility", RoR2_DLC1_CategoryChest2.iscCategoryChest2Utility_asset) ||
                        matchDirectorCard(card, "iscCategoryChestHealing", RoR2_Base_CategoryChest.iscCategoryChestHealing_asset) ||
                        matchDirectorCard(card, "iscCategoryChest2Healing", RoR2_DLC1_CategoryChest2.iscCategoryChest2Healing_asset))
                    {
                        Log.Debug($"Found category chest ({card.spawnCard}) in {dccs}");
                        return true;
                    }
                }
            }

            Log.Debug($"Did not find category chest in {dccs}");

            return false;
        }
    }
}
