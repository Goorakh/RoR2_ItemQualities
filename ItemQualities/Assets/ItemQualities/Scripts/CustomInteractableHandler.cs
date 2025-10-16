using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;

namespace ItemQualities
{
    static class CustomInteractableHandler
    {
        [SystemInitializer]
        static void Init()
        {
            const float QualityCategoryWeight = 2f;
            const string QualityCategoryName = "Quality Stuff";

            DirectorAPI.Helpers.AddNewInteractable(new DirectorAPI.DirectorCardHolder
            {
                Card = new DirectorCard
                {
                    spawnCard = ItemQualitiesContent.SpawnCards.QualityEquipmentBarrel,
                    selectionWeight = 2
                },
                InteractableCategorySelectionWeight = QualityCategoryWeight,
                CustomInteractableCategory = QualityCategoryName,
                InteractableCategory = DirectorAPI.InteractableCategory.Custom,
            }, containsEquipmentBarrels);

            DirectorAPI.Helpers.AddNewInteractable(new DirectorAPI.DirectorCardHolder
            {
                Card = new DirectorCard
                {
                    spawnCard = ItemQualitiesContent.SpawnCards.QualityChest2,
                    selectionWeight = 1
                },
                InteractableCategorySelectionWeight = QualityCategoryWeight,
                CustomInteractableCategory = QualityCategoryName,
                InteractableCategory = DirectorAPI.InteractableCategory.Custom,
            }, containsAnyCategoryChest);

            DirectorAPI.Helpers.AddNewInteractable(new DirectorAPI.DirectorCardHolder
            {
                Card = new DirectorCard
                {
                    spawnCard = ItemQualitiesContent.SpawnCards.QualityChest1,
                    selectionWeight = 2
                },
                InteractableCategorySelectionWeight = QualityCategoryWeight,
                CustomInteractableCategory = QualityCategoryName,
                InteractableCategory = DirectorAPI.InteractableCategory.Custom,
            }, containsAnyCategoryChest);
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
                        return true;
                    }
                }
            }

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
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
