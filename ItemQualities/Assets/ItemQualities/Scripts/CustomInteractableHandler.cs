using R2API;
using RoR2;

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
                    selectionWeight = 1
                },
                InteractableCategorySelectionWeight = QualityCategoryWeight,
                CustomInteractableCategory = QualityCategoryName,
                InteractableCategory = DirectorAPI.InteractableCategory.Custom,
            });

            DirectorAPI.Helpers.AddNewInteractable(new DirectorAPI.DirectorCardHolder
            {
                Card = new DirectorCard
                {
                    spawnCard = ItemQualitiesContent.SpawnCards.QualityChest2,
                    selectionWeight = 2
                },
                InteractableCategorySelectionWeight = QualityCategoryWeight,
                CustomInteractableCategory = QualityCategoryName,
                InteractableCategory = DirectorAPI.InteractableCategory.Custom,
            });
        }
    }
}
