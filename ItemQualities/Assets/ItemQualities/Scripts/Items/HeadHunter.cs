using R2API;
using RoR2;

namespace ItemQualities.Items
{
    static class HeadHunter
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            ItemQualityCounts headHunter = ItemQualitiesContent.ItemQualityGroups.HeadHunter.GetItemCountsEffective(sender.inventory);
            if (headHunter.TotalQualityCount > 0)
            {
                int eliteBuffCount = 0;
                foreach (BuffIndex buffIndex in BuffCatalog.eliteBuffIndices)
                {
                    if (sender.HasBuff(buffIndex))
                    {
                        eliteBuffCount++;
                    }
                }

                if (eliteBuffCount > 0)
                {
                    float damageIncreasePerEliteBuff = (0.2f * headHunter.UncommonCount) +
                                                       (0.4f * headHunter.RareCount) +
                                                       (0.8f * headHunter.EpicCount) +
                                                       (1.0f * headHunter.LegendaryCount);

                    args.damageMultAdd += damageIncreasePerEliteBuff * eliteBuffCount;
                }
            }
        }
    }
}
