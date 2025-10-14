using R2API;
using RoR2;

namespace ItemQualities
{
    static class CustomInteractableHandler
    {
        [SystemInitializer]
        static void Init()
        {
            DirectorAPI.Helpers.AddNewInteractable(new DirectorAPI.DirectorCardHolder
            {
                Card = new DirectorCard
                {
                    spawnCard = ItemQualitiesContent.SpawnCards.QualityEquipmentBarrel,
                    selectionWeight = 1
                },
                InteractableCategorySelectionWeight = 0.5f,
                CustomInteractableCategory = "Quality Stuff",
                InteractableCategory = DirectorAPI.InteractableCategory.Custom,
            });
        }
    }
}
