using R2API;
using RoR2;

namespace ItemQualities.Items
{
    static class GoldOnHurt
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
        }

        static void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            CharacterMaster master = sender ? sender.master : null;
            Inventory inventory = sender ? sender.inventory : null;

            ItemQualityCounts goldOnHurt = default;
            if (inventory)
            {
                goldOnHurt = ItemQualitiesContent.ItemQualityGroups.GoldOnHurt.GetItemCounts(inventory);
            }

            if (goldOnHurt.TotalCount > goldOnHurt.BaseItemCount)
            {
                uint currentMoney = master ? master.money : 0;
                if (currentMoney <= Run.instance.GetDifficultyScaledCost(25, Stage.instance.entryDifficultyCoefficient))
                {
                    args.armorAdd += (10f * goldOnHurt.UncommonCount) +
                                     (25f * goldOnHurt.RareCount) +
                                     (50f * goldOnHurt.EpicCount) +
                                     (100f * goldOnHurt.LegendaryCount);
                }
            }
        }
    }
}
