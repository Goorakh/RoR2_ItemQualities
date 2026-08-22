using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Utilities
{
    internal static class BossUtil
    {
        private static SceneIndex _solusWebSceneIndex = SceneIndex.Invalid;

        [SystemInitializer(typeof(SceneCatalog))]
        private static void Init()
        {
            _solusWebSceneIndex = SceneCatalog.FindSceneIndex("solusweb");
            if (_solusWebSceneIndex == SceneIndex.Invalid)
            {
                Log.Warning("Failed to find solusweb scene index");
            }
        }

        public static bool IsNonFinalPhase(BossGroup bossGroup)
        {
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
                    return true;
                default:
                    return false;
            }
        }

        public static Vector3 GetBestRewardPosition(BossGroup bossGroup)
        {
            if (bossGroup.dropPosition)
            {
                return bossGroup.dropPosition.position;
            }

            if (bossGroup.TryGetComponent(out SolusWebMissionController solusWebMissionController) &&
                solusWebMissionController.offeringMaster)
            {
                return MasterUtil.GetBestBodyCorePosition(solusWebMissionController.offeringMaster);
            }

            Vector3 bossPosition = bossGroup.transform.position;
            bool foundBossPosition = false;

            foreach (NetworkInstanceId memberInstanceId in bossGroup.combatSquad.memberHistory)
            {
                GameObject memberMasterObject = NetworkServer.active ? NetworkServer.FindLocalObject(memberInstanceId) : ClientScene.FindLocalObject(memberInstanceId);
                if (memberMasterObject && memberMasterObject.TryGetComponent(out CharacterMaster memberMaster))
                {
                    bossPosition = MasterUtil.GetBestBodyCorePosition(memberMaster);
                    foundBossPosition = true;

                    break;
                }
            }

            if (!foundBossPosition && bossGroup.TryGetComponent(out ScriptedCombatEncounter scriptedCombatEncounter))
            {
                Vector3 bestSpawnPosition = default;
                bool foundAnySpawnPosition = false;

                float bestSpawnCullChance = float.PositiveInfinity;
                float bestSpawnBaseHealth = float.NegativeInfinity;

                foreach (ScriptedCombatEncounter.SpawnInfo spawnInfo in scriptedCombatEncounter.spawns)
                {
                    if (!spawnInfo.explicitSpawnPosition || !spawnInfo.spawnCard)
                        continue;

                    CharacterMaster spawnMasterPrefab = spawnInfo.spawnCard.prefab ? spawnInfo.spawnCard.prefab.GetComponent<CharacterMaster>() : null;

                    float baseHealth = 0f;
                    if (spawnMasterPrefab &&
                        spawnMasterPrefab.bodyPrefab &&
                        spawnMasterPrefab.bodyPrefab.TryGetComponent(out CharacterBody bodyPrefab))
                    {
                        baseHealth = bodyPrefab.baseMaxHealth;
                    }

                    // Find the spawn with the highest health value, if there is a tie in health, choose the one with the lowest cull chance
                    if (!foundAnySpawnPosition ||
                        baseHealth > bestSpawnBaseHealth ||
                        (Mathf.Abs(baseHealth - bestSpawnBaseHealth) < 0.01f && spawnInfo.cullChance < bestSpawnCullChance))
                    {
                        bestSpawnBaseHealth = baseHealth;
                        bestSpawnCullChance = spawnInfo.cullChance;

                        bestSpawnPosition = MasterUtil.GetSpawnCorePosition(spawnMasterPrefab, spawnInfo.explicitSpawnPosition.position);
                        foundAnySpawnPosition = true;
                    }
                }

                if (foundAnySpawnPosition)
                {
                    bossPosition = bestSpawnPosition;
                    foundBossPosition = true;
                }
            }

            if (!foundBossPosition)
            {
                Log.Warning($"Failed to find reward position for boss {Util.GetGameObjectHierarchyName(bossGroup.gameObject)}, using group object position");
            }

            return bossPosition;
        }
    }
}
