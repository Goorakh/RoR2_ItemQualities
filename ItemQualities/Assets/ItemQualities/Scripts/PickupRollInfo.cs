using ItemQualities.Utilities.Extensions;
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
                if (master.inventory)
                {
                    clover = master.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Clover);
                }
            }
            else
            {
                ItemQualityCounts teamInventoryCloverCounts = default;

                foreach (EnemyInfoPanelInventoryProvider enemyInventoryProvider in InstanceTracker.GetInstancesList<EnemyInfoPanelInventoryProvider>())
                {
                    if (enemyInventoryProvider.inventory && enemyInventoryProvider.teamFilter && enemyInventoryProvider.teamFilter.teamIndex == teamAffiliation)
                    {
                        teamInventoryCloverCounts += enemyInventoryProvider.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Clover);
                    }
                }

                clover += teamInventoryCloverCounts;

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

                    clover += teammateMaster.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Clover) - teamInventoryCloverCounts;
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
