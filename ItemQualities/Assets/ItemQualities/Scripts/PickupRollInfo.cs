using ItemQualities.Utilities.Extensions;
using RoR2;

namespace ItemQualities
{
    public readonly struct PickupRollInfo
    {
        public readonly CharacterMaster Master;

        public readonly TeamIndex TeamAffiliation;

        public readonly int Luck;

        public readonly bool IsPlayerAffiliation => TeamAffiliation == TeamIndex.Player || (Master && Master.playerCharacterMasterController);

        public PickupRollInfo(CharacterMaster master, TeamIndex teamAffiliation) : this()
        {
            Master = master;
            TeamAffiliation = teamAffiliation;

            bool isPlayer = IsPlayerAffiliation;

            ItemQualityCounts clover = ItemQualityCounts.zero;
            if (master)
            {
                if (master.inventory)
                {
                    clover = master.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Clover);
                }
            }
            else
            {
                ItemQualityCounts teamInventoryCloverCounts = ItemQualityCounts.zero;

                foreach (EnemyInfoPanelInventoryProvider enemyInventoryProvider in InstanceTracker.GetInstancesList<EnemyInfoPanelInventoryProvider>())
                {
                    if (enemyInventoryProvider.inventory && enemyInventoryProvider.teamFilter && enemyInventoryProvider.teamFilter.teamIndex == teamAffiliation)
                    {
                        teamInventoryCloverCounts += enemyInventoryProvider.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Clover);
                    }
                }

                bool hasAnyTeamInventoryClovers = teamInventoryCloverCounts.TotalQualityCount > 0;
                if (hasAnyTeamInventoryClovers)
                {
                    clover += teamInventoryCloverCounts;
                }

                foreach (CharacterMaster teammateMaster in CharacterMaster.readOnlyInstancesList)
                {
                    if (teammateMaster.teamIndex != teamAffiliation || !teammateMaster.inventory)
                        continue;

                    if (isPlayer)
                    {
                        PlayerCharacterMasterController playerMaster = teammateMaster.playerCharacterMasterController;
                        if (!playerMaster || !playerMaster.isConnected)
                            continue;
                    }
                    else
                    {
                        if (!teammateMaster.hasBody)
                            continue;
                    }

                    ItemQualityCounts teammateCloverCounts = teammateMaster.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Clover);
                    if (hasAnyTeamInventoryClovers)
                    {
                        for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                        {
                            if (teammateCloverCounts[qualityTier] >= teamInventoryCloverCounts[qualityTier])
                            {
                                teammateCloverCounts[qualityTier] -= teamInventoryCloverCounts[qualityTier];
                            }
                        }
                    }

                    clover += teammateCloverCounts;
                }
            }

            int qualityLuck = (1 * clover.UncommonCount) +
                              (2 * clover.RareCount) +
                              (3 * clover.EpicCount) +
                              (5 * clover.LegendaryCount);

            Luck = qualityLuck;
        }
    };
}
