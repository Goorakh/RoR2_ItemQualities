using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class WarCryOnMultiKill
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender.HasBuff(RoR2Content.Buffs.WarCryBuff) || sender.HasBuff(RoR2Content.Buffs.TeamWarCry))
            {
                ItemQualityCounts warCryOnMultiKill = ItemQualitiesContent.ItemQualityGroups.WarCryOnMultiKill.GetItemCountsEffective(sender.inventory);
                BuffQualityCounts multikillWarCryBuff = ItemQualitiesContent.BuffQualityGroups.MultikillWarCryBuff.GetBuffCounts(sender);
                if (warCryOnMultiKill.TotalQualityCount > 0 && multikillWarCryBuff.TotalQualityCount > 0)
                {
                    float damageIncreasePerBuff = (0.01f * warCryOnMultiKill.UncommonCount) +
                                                  (0.02f * warCryOnMultiKill.RareCount) +
                                                  (0.03f * warCryOnMultiKill.EpicCount) +
                                                  (0.05f * warCryOnMultiKill.LegendaryCount);

                    args.damageMultAdd += damageIncreasePerBuff * multikillWarCryBuff.TotalQualityCount;
                }
            }
        }
    }
}
