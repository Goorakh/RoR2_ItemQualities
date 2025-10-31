using RoR2;

namespace ItemQualities
{
    public readonly struct PickupRollInfo
    {
        public readonly CharacterMaster Master { get; }

        public readonly TeamIndex TeamAffiliation { get; }

        public readonly int Luck { get; }

        public readonly bool IsPlayerAffiliation => TeamAffiliation == TeamIndex.Player || (Master && Master.playerCharacterMasterController);

        public PickupRollInfo(CharacterMaster master, TeamIndex teamAffiliation) : this()
        {
            Master = master;
            TeamAffiliation = teamAffiliation;

            bool isPlayer = IsPlayerAffiliation;

            ItemQualityCounts clover = default;
            if (master)
            {
                clover = ItemQualitiesContent.ItemQualityGroups.Clover.GetItemCounts(master.inventory);
            }
            else
            {
                ItemQualityCounts teamInventoryCloverCounts = default;

                foreach (EnemyInfoPanelInventoryProvider enemyInventoryProvider in InstanceTracker.GetInstancesList<EnemyInfoPanelInventoryProvider>())
                {
                    if (enemyInventoryProvider.teamFilter && enemyInventoryProvider.teamFilter.teamIndex == teamAffiliation)
                    {
                        teamInventoryCloverCounts += ItemQualitiesContent.ItemQualityGroups.Clover.GetItemCounts(enemyInventoryProvider.inventory);
                    }
                }

                clover += teamInventoryCloverCounts;

                foreach (CharacterMaster teammateMaster in CharacterMaster.readOnlyInstancesList)
                {
                    if (teammateMaster.teamIndex != teamAffiliation)
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

                    clover += ItemQualitiesContent.ItemQualityGroups.Clover.GetItemCounts(teammateMaster.inventory) - teamInventoryCloverCounts;
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
