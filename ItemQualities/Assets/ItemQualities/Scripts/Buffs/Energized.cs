using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;

namespace ItemQualities.Buffs
{
    internal static class Energized
    {
        [SystemInitializer]
        private static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
        }

        private static void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender || !sender.inventory)
                return;

            BuffQualityCounts energized = sender.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.Energized);
            ItemQualityCounts energizedOnEquipmentUse = sender.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.EnergizedOnEquipmentUse);
            if (energized.TotalQualityCount > 0)
            {
                if (energizedOnEquipmentUse.TotalQualityCount == 0)
                    energizedOnEquipmentUse.UncommonCount = 1;

                // Includes +70% from normal warhorn
                float bonusAttackSpeed = (0.1f * energizedOnEquipmentUse.UncommonCount) +
                                         (0.3f * energizedOnEquipmentUse.RareCount) +
                                         (0.6f * energizedOnEquipmentUse.EpicCount) +
                                         (1.0f * energizedOnEquipmentUse.LegendaryCount);

                float cooldownReduction = 0.1f + (0.1f * energizedOnEquipmentUse.UncommonCount) +
                                                 (0.3f * energizedOnEquipmentUse.RareCount) +
                                                 (0.5f * energizedOnEquipmentUse.EpicCount) +
                                                 (0.9f * energizedOnEquipmentUse.LegendaryCount);

                args.attackSpeedMultAdd += bonusAttackSpeed;
                args.allSkills.cooldownReductionMultAdd += cooldownReduction;
            }
        }
    }
}
