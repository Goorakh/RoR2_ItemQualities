using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;

namespace ItemQualities.Items
{
    internal static class Pearl
    {
        [SystemInitializer]
        private static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += GetStatCoefficients;
        }

        private static void GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender || !sender.inventory)
                return;

            ItemQualityCounts pearl = sender.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Pearl);

            if (pearl.TotalQualityCount > 0)
            {
                args.baseRegenAdd += pearl.UncommonCount * 3f;
                args.levelRegenAdd += pearl.UncommonCount * 0.6f;
                args.baseHealthAdd += pearl.UncommonCount * 100;

                args.baseShieldAdd += pearl.RareCount > 0 ? 50 : 0;
                args.shieldMultAdd += pearl.RareCount * 0.8f;

                if (sender.armor > 0)
                {
                    args.armorTotalMult += pearl.EpicCount * 0.5f;
                }

                args.damageMultAdd += pearl.LegendaryCount * 0.4f;
            }
        }
    }
}
