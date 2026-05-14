using ItemQualities.ModCompatibility;
using ItemQualities.Orbs;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.Orbs;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    internal static class FlatHealth
    {
        [SystemInitializer]
        private static void Init()
        {
            ObjectiveEvents.OnFinalMoonPillarChargedGlobal += onFinalMoonPillarChargedGlobal;
            ObjectiveEvents.OnFinalVoidStagePillarChargedServer += onFinalVoidStagePillarChargedServer;
            ArenaMissionController.onBeatArena += onBeatArena;
            BossGroup.onBossGroupDefeatedServer += onBossGroupDefeatedServer;

            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        private static void tryDispatchSteakReward(CharacterBody targetBody, Vector3 origin)
        {
            if (!targetBody || !targetBody.healthComponent || !targetBody.healthComponent.alive)
                return;

            Inventory inventory = targetBody.inventory;
            if (!inventory)
                return;

            ItemQualityCounts flatHealth = inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.FlatHealth);
            if (flatHealth.TotalQualityCount > 0)
            {
                float steakBonus = (40f * flatHealth.UncommonCount) +
                                   (80f * flatHealth.RareCount) +
                                   (130f * flatHealth.EpicCount) +
                                   (200f * flatHealth.LegendaryCount);

                if (steakBonus > 0f)
                {
                    SteakOrb orb = new SteakOrb
                    {
                        origin = origin,
                        target = targetBody.mainHurtBox,
                        SteakBonus = steakBonus
                    };

                    OrbManager.instance.AddOrb(orb);
                }
            }
        }

        private static void tryDispatchSteakRewards(TeamIndex targetTeam, Vector3 origin)
        {
            foreach (TeamComponent teamComponent in TeamComponent.GetTeamMembers(targetTeam))
            {
                tryDispatchSteakReward(teamComponent.body, origin);
            }
        }

        private static void onFinalMoonPillarChargedGlobal(HoldoutZoneController pillarHoldoutZone)
        {
            if (!NetworkServer.active)
                return;

            TeamIndex chargingTeam = pillarHoldoutZone.chargingTeam;
            if (chargingTeam == TeamIndex.None)
                return;

            tryDispatchSteakRewards(chargingTeam, pillarHoldoutZone.transform.position + new Vector3(0f, 2f, 0f));
        }

        private static void onFinalVoidStagePillarChargedServer(HoldoutZoneController pillarHoldoutZone)
        {
            if (!NetworkServer.active)
                return;

            TeamIndex chargingTeam = pillarHoldoutZone.chargingTeam;
            if (chargingTeam == TeamIndex.None)
                return;

            tryDispatchSteakRewards(chargingTeam, pillarHoldoutZone.transform.position + new Vector3(0f, 2f, 0f));
        }

        private static void onBeatArena()
        {
            if (!NetworkServer.active || !ArenaMissionController.instance)
                return;

            GameObject lastWard = null;
            if (ArenaMissionController.instance.nullWards != null && ArenaMissionController.instance.nullWards.Length > 0)
            {
                lastWard = ArenaMissionController.instance.nullWards[^1];
            }

            HoldoutZoneController lastWardHoldoutZone = lastWard ? lastWard.GetComponent<HoldoutZoneController>() : null;

            TeamIndex chargingTeam = lastWardHoldoutZone ? lastWardHoldoutZone.chargingTeam : TeamIndex.None;
            if (chargingTeam == TeamIndex.None)
                return;

            Vector3 position = lastWard ? lastWard.transform.position : ArenaMissionController.instance.transform.position;

            tryDispatchSteakRewards(chargingTeam, position);
        }

        private static void onBossGroupDefeatedServer(BossGroup bossGroup)
        {
            // Ignore all boss groups that aren't the final phase
            // TODO: Find a better way to do this
            switch (bossGroup.name)
            {
                // False Son, only phase 3
                case "FSBF Phase1":
                case "FSBF Phase2":

                // Mithrix, only phase 4
                case "BrotherEncounter, Phase 1":
                case "BrotherEncounter, Phase 2":
                case "BrotherEncounter, Phase 3":

                // Voidling, only phase 3
                case "VoidRaidCrabCombatEncounter Phase 1":
                case "VoidRaidCrabCombatEncounter Phase 2":
                    Log.Debug($"Non-final phase BossGroup {Util.GetGameObjectHierarchyName(bossGroup.gameObject)} defeated, ignoring");
                    return;
            }

            Log.Debug($"BossGroup {Util.GetGameObjectHierarchyName(bossGroup.gameObject)} defeated");

            TeamIndex bossTeam = TeamIndex.None;
            Vector3 bossPosition = bossGroup.transform.position;
            bool foundBossPosition = false;

            foreach (NetworkInstanceId memberInstanceId in bossGroup.combatSquad.memberHistory)
            {
                GameObject memberMasterObject = NetworkServer.active ? NetworkServer.FindLocalObject(memberInstanceId) : ClientScene.FindLocalObject(memberInstanceId);
                if (memberMasterObject && memberMasterObject.TryGetComponent(out CharacterMaster memberMaster))
                {
                    bossTeam = memberMaster.teamIndex;

                    if (memberMaster.lostBodyToDeath)
                    {
                        bossPosition = memberMaster.deathFootPosition;
                        foundBossPosition = true;
                    }

                    break;
                }
            }

            if (bossGroup.TryGetComponent(out ScriptedCombatEncounter scriptedCombatEncounter))
            {
                bossTeam = scriptedCombatEncounter.teamIndex;

                if (!foundBossPosition)
                {
                    Vector3? bestSpawnPosition = null;

                    float bestSpawnCullChance = float.PositiveInfinity;
                    float bestSpawnBaseHealth = float.NegativeInfinity;

                    foreach (ScriptedCombatEncounter.SpawnInfo spawnInfo in scriptedCombatEncounter.spawns)
                    {
                        if (!spawnInfo.explicitSpawnPosition)
                            continue;

                        float baseHealth = 0f;
                        if (spawnInfo.spawnCard &&
                            spawnInfo.spawnCard.prefab &&
                            spawnInfo.spawnCard.prefab.TryGetComponent(out CharacterMaster masterPrefab) &&
                            masterPrefab.bodyPrefab &&
                            masterPrefab.bodyPrefab.TryGetComponent(out CharacterBody bodyPrefab))
                        {
                            baseHealth = bodyPrefab.baseMaxHealth;
                        }

                        if (!bestSpawnPosition.HasValue ||
                            baseHealth > bestSpawnBaseHealth ||
                            (Mathf.Abs(baseHealth - bestSpawnBaseHealth) < 0.01f && spawnInfo.cullChance < bestSpawnCullChance))
                        {
                            bestSpawnBaseHealth = baseHealth;
                            bestSpawnCullChance = spawnInfo.cullChance;

                            bestSpawnPosition = spawnInfo.explicitSpawnPosition.position;
                        }
                    }

                    if (bestSpawnPosition.HasValue)
                    {
                        bossPosition = bestSpawnPosition.Value;
                        foundBossPosition = true;
                    }
                }
            }

            TeamMask rewardTeams;
            if (bossGroup.TryGetComponent(out HoldoutZoneController holdoutZoneController))
            {
                rewardTeams = TeamMask.none;
                rewardTeams.AddTeam(holdoutZoneController.chargingTeam);
            }
            else if (bossTeam != TeamIndex.None)
            {
                rewardTeams = TeamMask.allButNeutral;
                rewardTeams.RemoveTeam(bossTeam);
            }
            else
            {
                // Fallback to Player team if we can't determine the team
                rewardTeams = TeamMask.none;
                rewardTeams.AddTeam(TeamIndex.Player);
            }

            Vector3 orbSpawnPosition = bossGroup.dropPosition ? bossGroup.dropPosition.position : bossPosition;

            for (TeamIndex teamIndex = 0; (int)teamIndex < TeamsAPICompat.TeamsCount; teamIndex++)
            {
                tryDispatchSteakRewards(teamIndex, orbSpawnPosition);
            }
        }

        private static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender.inventory)
                return;

            ItemQualityCounts flatHealth = sender.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.FlatHealth);

            if (flatHealth.TotalQualityCount > 0)
            {
                if (sender.master.TryGetComponentCached(out CharacterMasterExtraStatsTracker masterExtraStatsTracker))
                {
                    args.baseHealthAdd += masterExtraStatsTracker.SteakBonus;
                }
            }
        }
    }
}
