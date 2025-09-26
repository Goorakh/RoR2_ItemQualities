using R2API;
using RoR2;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class FlatHealth
    {
        [SystemInitializer]
        static void Init()
        {
            TeleporterInteraction.onTeleporterChargedGlobal += onTeleporterChargedGlobal;
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        static void onTeleporterChargedGlobal(TeleporterInteraction teleporterInteraction)
        {
            if (!NetworkServer.active)
                return;

            HoldoutZoneController holdoutZoneController = teleporterInteraction ? teleporterInteraction.holdoutZoneController : null;

            TeamIndex chargingTeam = holdoutZoneController ? holdoutZoneController.chargingTeam : TeamIndex.None;

            foreach (TeamComponent teamComponent in TeamComponent.GetTeamMembers(chargingTeam))
            {
                CharacterBody body = teamComponent ? teamComponent.body : null;
                Inventory inventory = body ? body.inventory : null;
                CharacterMaster master = body ? body.master : null;

                ItemQualityCounts flatHealth = default;
                if (inventory)
                {
                    flatHealth = ItemQualitiesContent.ItemQualityGroups.FlatHealth.GetItemCounts(inventory);
                }

                if (flatHealth.TotalCount > flatHealth.BaseItemCount)
                {
                    float steakBonus = (25f * flatHealth.UncommonCount) +
                                       (50f * flatHealth.RareCount) +
                                       (100f * flatHealth.EpicCount) +
                                       (250f * flatHealth.LegendaryCount);

                    if (master && master.TryGetComponent(out CharacterMasterExtraStatsTracker masterExtraStatsTracker))
                    {
                        masterExtraStatsTracker.SteakBonus += steakBonus;
                    }
                }
            }
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            Inventory inventory = sender ? sender.inventory : null;
            CharacterMaster master = sender ? sender.master : null;

            ItemQualityCounts flatHealth = default;
            if (inventory)
            {
                flatHealth = ItemQualitiesContent.ItemQualityGroups.FlatHealth.GetItemCounts(inventory);
            }

            if (flatHealth.TotalCount > flatHealth.BaseItemCount)
            {
                if (master && master.TryGetComponent(out CharacterMasterExtraStatsTracker masterExtraStatsTracker))
                {
                    args.baseHealthAdd += masterExtraStatsTracker.SteakBonus;
                }
            }
        }
    }
}
