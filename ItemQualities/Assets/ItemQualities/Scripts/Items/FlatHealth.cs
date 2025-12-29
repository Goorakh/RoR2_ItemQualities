using ItemQualities.Orbs;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.Orbs;
using UnityEngine;
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

                if (!body || !inventory)
                    continue;

                ItemQualityCounts flatHealth = default;
                if (inventory)
                {
                    flatHealth = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.FlatHealth);
                }

                if (flatHealth.TotalQualityCount > 0)
                {
                    float steakBonus = (25f * flatHealth.UncommonCount) +
                                       (50f * flatHealth.RareCount) +
                                       (100f * flatHealth.EpicCount) +
                                       (250f * flatHealth.LegendaryCount);

                    if (steakBonus > 0f)
                    {
                        SteakOrb orb = new SteakOrb
                        {
                            origin = teleporterInteraction.transform.position + new Vector3(0f, 2f, 0f),
                            target = body.mainHurtBox,
                            SteakBonus = steakBonus
                        };

                        OrbManager.instance.AddOrb(orb);
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
                flatHealth = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.FlatHealth);
            }

            if (flatHealth.TotalQualityCount > 0)
            {
                if (master && master.TryGetComponent(out CharacterMasterExtraStatsTracker masterExtraStatsTracker))
                {
                    args.baseHealthAdd += masterExtraStatsTracker.SteakBonus;
                }
            }
        }
    }
}
