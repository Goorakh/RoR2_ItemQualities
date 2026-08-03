using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    internal static class ShinyPearl
    {
        [InitDuringStartupPhase(GameInitPhase.PostProgressBar)]
        private static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;

            On.RoR2.Util.CheckRoll_float_CharacterMaster += Util_CheckRoll_float_CharacterMaster;
            On.RoR2.Util.CheckRoll0To1_float_CharacterMaster += Util_CheckRoll0To1_float_CharacterMaster;
        }

        private static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            int shinyPearlBuffCount = sender.GetBuffCount(ItemQualitiesContent.Buffs.ShinyPearlLuck);
            args.luckAdd += shinyPearlBuffCount * 0.01f;
        }

        private static bool Util_CheckRoll0To1_float_CharacterMaster(On.RoR2.Util.orig_CheckRoll0To1_float_CharacterMaster orig, float percentChance, CharacterMaster master)
        {
            bool result = orig(percentChance, master);
            
            if (percentChance > 0f && percentChance < 1f && master)
            {
                onRollResult(master, result);
            }

            return result;
        }

        private static bool Util_CheckRoll_float_CharacterMaster(On.RoR2.Util.orig_CheckRoll_float_CharacterMaster orig, float percentChance, CharacterMaster master)
        {
            bool result = orig(percentChance, master);

            if (percentChance > 0f && percentChance < 100f && master)
            {
                onRollResult(master, result);
            }

            return result;
        }

        private static void onRollResult(CharacterMaster master, bool success)
        {
            CharacterBody body = master.GetBody();
            if (!body)
            {
                return;
            }

            ItemQualityCounts shinyPearl = master.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ShinyPearl);
            QualityTier qualityTier = shinyPearl.HighestQuality;

            if (!success)
            {
                if (shinyPearl.TotalQualityCount > 0)
                {
                    int desiredBuffsToAdd = qualityTier switch
                    {
                        QualityTier.Uncommon => 1,
                        QualityTier.Rare => 2,
                        QualityTier.Epic => 4,
                        QualityTier.Legendary => 6,
                        _ => throw new NotImplementedException(),
                    };

                    float buffDuration = qualityTier switch
                    {
                        QualityTier.Uncommon => 10f,
                        QualityTier.Rare => 15f,
                        QualityTier.Epic => 20f,
                        QualityTier.Legendary => 30f,
                        _ => throw new NotImplementedException(),
                    };

                    int maxStacks = (shinyPearl.UncommonCount * 100) +
                                    (shinyPearl.RareCount * 200) +
                                    (shinyPearl.EpicCount * 400) +
                                    (shinyPearl.LegendaryCount * 600);

                    for (int i = 0; i < desiredBuffsToAdd; i++)
                    {
                        body.AddTimedBuff(ItemQualitiesContent.Buffs.ShinyPearlLuck, buffDuration, maxStacks);
                    }
                }
            }
            else
            {
                int buffCount = body.GetBuffCount(ItemQualitiesContent.Buffs.ShinyPearlLuck);
                int buffsToRemove = Mathf.Min(1, buffCount);
                for (int i = 0; i < buffsToRemove; i++)
                {
                    body.RemoveOldestTimedBuff(ItemQualitiesContent.Buffs.ShinyPearlLuck);
                }
            }
        }
    }
}
