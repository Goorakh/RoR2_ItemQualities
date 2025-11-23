using R2API;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class BonusGoldPackOnKill
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
            On.RoR2.CharacterMaster.UpdateMoneyBasedItems += giveBuffsOnGoldChange;
        }

        private static void giveBuffsOnGoldChange(On.RoR2.CharacterMaster.orig_UpdateMoneyBasedItems orig, CharacterMaster self)
        {
            orig(self);

            CharacterBody body = self.GetBody();
            if (!body)
                return;

            ItemQualityCounts bonusGoldPackOnKill = ItemQualitiesContent.ItemQualityGroups.BonusGoldPackOnKill.GetItemCountsEffective(body.master.inventory);
            if (bonusGoldPackOnKill.TotalQualityCount > 0)
            {
                float multiplier = 0;
                switch (bonusGoldPackOnKill.HighestQuality)
                {
                    case QualityTier.Uncommon:
                        multiplier = 2f;
                        break;
                    case QualityTier.Rare:
                        multiplier = 3f;
                        break;
                    case QualityTier.Epic:
                        multiplier = 3.5f;
                        break;
                    case QualityTier.Legendary:
                        multiplier = 4f;
                        break;
                }

                float max = (bonusGoldPackOnKill.UncommonCount * 20) +
                            (bonusGoldPackOnKill.RareCount * 40) +
                            (bonusGoldPackOnKill.EpicCount * 60) +
                            (bonusGoldPackOnKill.LegendaryCount * 100);

                body.SetBuffCount(ItemQualitiesContent.Buffs.GoldenGun.buffIndex, (int)Math.Min(multiplier * body.master.money / Run.instance.GetDifficultyScaledCost(25), max));
            }
        }

        private static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender)
                return;

            args.baseDamageAdd += 0.01f * sender.baseDamage * sender.GetBuffCount(ItemQualitiesContent.Buffs.GoldenGun);
        }
    }
}
