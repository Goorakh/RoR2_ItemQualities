using ItemQualities.Utilities.Extensions;
using RoR2;

namespace ItemQualities.Utilities
{
    public static class ItemQualityUtils
    {
        public static ItemQualityCounts GetTeamItemCounts(ItemQualityGroupIndex itemGroupIndex, TeamIndex teamIndex, bool requireAlive, bool requireConnected = true)
        {
            return GetTeamItemCounts(QualityCatalog.GetItemQualityGroup(itemGroupIndex), teamIndex, requireAlive, requireConnected);
        }

        public static ItemQualityCounts GetTeamItemCounts(ItemQualityGroup itemGroup, TeamIndex teamIndex, bool requireAlive, bool requireConnected = true)
        {
            if (!itemGroup)
                return default;

            ItemQualityCounts itemCounts = default;

            foreach (CharacterMaster master in CharacterMaster.readOnlyInstancesList)
            {
                if (!master)
                    continue;

                if (master.teamIndex != teamIndex)
                    continue;

                CharacterBody body = master.GetBody();
                if (requireAlive && (!body || !body.healthComponent || !body.healthComponent.alive))
                    continue;

                if (requireConnected && (!master.playerCharacterMasterController || !master.playerCharacterMasterController.isConnected))
                    continue;

                itemCounts += master.inventory.GetItemCountsEffective(itemGroup);
            }

            return itemCounts;
        }
    }
}
