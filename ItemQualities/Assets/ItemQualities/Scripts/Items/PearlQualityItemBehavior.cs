using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;

namespace ItemQualities.Items
{
    public sealed class PearlQualityItemBehavior : QualityItemBodyBehavior
    {
		[SystemInitializer]
		private static void Init()
		{
            RecalculateStatsAPI.GetStatCoefficients += GetStatCoefficients;
		}
		
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server | QualityItemBehaviorUsageFlags.Client)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.Pearl;

        private void OnEnable()
        {
            Body.onRecalculateStats += RecalculateStats;
        }

        private void OnDisable()
        {
            Body.onRecalculateStats -= RecalculateStats;
        }

        private void RecalculateStats(CharacterBody body)
        {
            if (!body || !body.inventory)
                return;

            ItemQualityCounts pearl = body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Pearl);

            if (pearl.TotalQualityCount > 0 && body.armor > 0)
            {
                body.armor *= 1 + (pearl.EpicCount * 0.5f);
            }
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
                args.healthMultAdd += pearl.UncommonCount * 0.2f;

                args.baseShieldAdd += pearl.RareCount > 0 ? 50 : 0;
                args.shieldMultAdd += pearl.RareCount * 0.8f;

                args.damageMultAdd += pearl.LegendaryCount * 0.4f;
            }
        }
    }
}
