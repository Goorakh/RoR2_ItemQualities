using RoR2;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class TPHealingNova
    {
        static SceneIndex _limboSceneIndex = SceneIndex.Invalid;

        [SystemInitializer(typeof(SceneCatalog))]
        static void Init()
        {
            _limboSceneIndex = SceneCatalog.FindSceneIndex("limbo");
            if (_limboSceneIndex == SceneIndex.Invalid)
            {
                Log.Warning("Failed to find limbo scene index");
            }

            On.EntityStates.Missions.Goldshores.GoldshoresBossfight.SpawnBoss += GoldshoresBossfight_SpawnBoss;

            Stage.onServerStageBegin += onServerStageBegin;
        }

        static void GoldshoresBossfight_SpawnBoss(On.EntityStates.Missions.Goldshores.GoldshoresBossfight.orig_SpawnBoss orig, EntityStates.Missions.Goldshores.GoldshoresBossfight self)
        {
            orig(self);
            tryInitializeGoldshoresNovaManagers(self.scriptedCombatEncounter);
        }

        static void onServerStageBegin(Stage stage)
        {
            if (!NetworkServer.active || !stage)
                return;

            SceneIndex sceneIndex = stage.sceneDef ? stage.sceneDef.sceneDefIndex : SceneIndex.Invalid;
            stage.StartCoroutine(tryInitializeStageNovaManagers(sceneIndex));
        }

        static GameObject createHealNovaManager(BossGroup bossGroup, Transform parent, Vector3 position, float radius)
        {
            GameObject healNovaManager = new GameObject("HealNovaManager");
            healNovaManager.transform.SetParent(parent);
            healNovaManager.transform.position = position;

            BossArenaHealNovaManager phaseHealNovaManager = healNovaManager.AddComponent<BossArenaHealNovaManager>();
            phaseHealNovaManager.WatchingBossGroup = bossGroup;
            phaseHealNovaManager.ArenaRadius = radius;
            return healNovaManager;
        }

        static IEnumerator tryInitializeStageNovaManagers(SceneIndex sceneIndex)
        {
            yield return new WaitForFixedUpdate();
            tryInitializeMoonNovaManagers();

            if (VoidRaidGauntletController.instance)
            {
                VoidRaidGauntletController.instance.StartCoroutine(tryInitializeVoidRaidNovaManagers());
            }

            tryInitializeMeridianNovaManagers();
            tryInitializeHauntNovaManagers();
            tryInitializeHeartNovaManagers();

            if (sceneIndex != SceneIndex.Invalid)
            {
                if (sceneIndex == _limboSceneIndex)
                {
                    tryInitializeLimboNovaManagers();
                }
            }
        }

        static void tryInitializeHeartNovaManagers()
        {
            GameObject solusWebMissionController = GameObject.Find("SolusWebMissionController");
            if (!solusWebMissionController)
                return;

            GameObject solusHeartSpawn = GameObject.Find("SolusHeartSpawn");
            if (!solusHeartSpawn)
                return;

            Vector3 arenaCenterPosition = solusHeartSpawn.transform.position;

            BossGroup bossGroup = solusWebMissionController.GetComponent<BossGroup>();
            if (!bossGroup)
                return;

            createHealNovaManager(bossGroup, bossGroup.transform, arenaCenterPosition, 150f);
        }

        static void tryInitializeHauntNovaManagers()
        {
            if (!SolutionalHauntReferences.singletonInstance)
                return;

            GameObject arenaCenter = GameObject.Find("HOLDER2: Geo and Spawnpoints (prev ArenaCenter)");
            if (!arenaCenter)
                return;

            Vector3 arenaCenterPosition = arenaCenter.transform.position;

            BossGroup bossGroup = SolutionalHauntReferences.singletonInstance.solusWingBossGroup;
            if (!bossGroup)
                return;

            createHealNovaManager(bossGroup, bossGroup.transform, arenaCenterPosition, 250f);
        }

        static void tryInitializeMoonNovaManagers()
        {
            if (!SceneInfo.instance)
                return;

            Transform brotherMissionController = SceneInfo.instance.transform.Find("BrotherMissionController");
            if (!brotherMissionController)
                return;

            Vector3 arenaCenterPosition = brotherMissionController.position;
            if (SceneInfo.instance.TryGetComponent(out ChildLocator sceneChildLocator))
            {
                Transform centerOfArenaTransform = sceneChildLocator.FindChild("CenterOfArena");
                if (centerOfArenaTransform)
                {
                    arenaCenterPosition = centerOfArenaTransform.position;
                }
            }

            BossGroup[] bossGroups = brotherMissionController.GetComponentsInChildren<BossGroup>(true);
            if (bossGroups.Length > 0)
            {
                foreach (BossGroup bossGroup in bossGroups)
                {
                    createHealNovaManager(bossGroup, bossGroup.transform, arenaCenterPosition, 260f);
                }
            }
        }

        static IEnumerator tryInitializeVoidRaidNovaManagers()
        {
            while (VoidRaidGauntletController.instance && !VoidRaidGauntletController.instance.hasShuffled)
            {
                yield return null;
            }

            if (!VoidRaidGauntletController.instance)
                yield break;

            ScriptedCombatEncounter[] phaseEncounters = VoidRaidGauntletController.instance.phaseEncounters;

            List<VoidRaidGauntletController.DonutInfo> arenas = new List<VoidRaidGauntletController.DonutInfo>();
            arenas.Add(VoidRaidGauntletController.instance.initialDonut);
            arenas.AddRange(VoidRaidGauntletController.instance.followingDonuts);

            for (int i = 0; i < phaseEncounters.Length; i++)
            {
                VoidRaidGauntletController.DonutInfo arena = arenas[i];
                ScriptedCombatEncounter phaseEncounter = phaseEncounters[i];

                if (!arena?.root || !phaseEncounter || !phaseEncounter.TryGetComponent(out BossGroup phaseBossGroup))
                {
                    Log.Warning($"Invalid phase encounter at index {i}");
                    continue;
                }

                Vector3 arenaCenterPosition = arena.root.transform.position;

                // Somehow hopoo managed to zero out all the root object positions except for one (RaidAL), so hardcoded position it is. Yay!
                switch (arena.root.name)
                {
                    case "RaidBB":
                        arenaCenterPosition = new Vector3(-4000f, 0f, -4000f);
                        break;
                    case "RaidGP":
                        arenaCenterPosition = new Vector3(0f, 0f, 4000f);
                        break;
                    case "RaidSG":
                        arenaCenterPosition = new Vector3(-4000f, 0f, 0f);
                        break;
                    case "RaidDC":
                        arenaCenterPosition = new Vector3(0f, 0f, -4000f);
                        break;
                }

                createHealNovaManager(phaseBossGroup, arena.root.transform, arenaCenterPosition, 200f);
            }
        }

        static void tryInitializeMeridianNovaManagers()
        {
            if (!MeridianEventTriggerInteraction.instance)
                return;

            if (MeridianEventTriggerInteraction.instance.TryGetComponent(out ChildLocator childLocator))
            {
                tryCreateNovaManagerForPhase("Phase1", 175f);
                tryCreateNovaManagerForPhase("Phase2", 115f);
                tryCreateNovaManagerForPhase("Phase3", 75f);

                void tryCreateNovaManagerForPhase(string phaseChildString, float arenaRadius)
                {
                    GameObject phaseObject = childLocator.FindChildGameObject(phaseChildString);
                    if (!phaseObject)
                    {
                        Log.Warning($"Failed to find phase object '{phaseChildString}'");
                        return;
                    }

                    if (!phaseObject.TryGetComponent(out ScriptedCombatEncounter phaseEncounter) || !phaseObject.TryGetComponent(out BossGroup phaseBossGroup))
                    {
                        Log.Warning($"Phase object {Util.GetGameObjectHierarchyName(phaseObject)} is missing boss encounter components");
                        return;
                    }

                    Vector3 arenaCenterPosition = phaseObject.transform.position;
                    foreach (ScriptedCombatEncounter.SpawnInfo spawnInfo in phaseEncounter.spawns)
                    {
                        if (spawnInfo.explicitSpawnPosition)
                        {
                            arenaCenterPosition = spawnInfo.explicitSpawnPosition.position;
                            break;
                        }
                    }

                    createHealNovaManager(phaseBossGroup, phaseObject.transform, arenaCenterPosition, arenaRadius);
                }
            }
        }

        static void tryInitializeGoldshoresNovaManagers(ScriptedCombatEncounter combatEncounter)
        {
            if (combatEncounter && combatEncounter.TryGetComponent(out BossGroup bossGroup))
            {
                createHealNovaManager(bossGroup, combatEncounter.transform, new Vector3(0f, -8.5f, 0f), 200f);
            }
        }

        static void tryInitializeLimboNovaManagers()
        {
            GameObject scavLunarEncounter = GameObject.Find("ScavLunarEncounter");
            if (!scavLunarEncounter)
                return;

            if (scavLunarEncounter.TryGetComponent(out BossGroup scavLunarBossGroup))
            {
                GameObject healNovaManager = createHealNovaManager(scavLunarBossGroup, scavLunarEncounter.transform, new Vector3(23.64539f, -6.5077f, 24.37448f), 300f);
                healNovaManager.transform.eulerAngles = new Vector3(0.7121587f, 0f, 4.222344f);
            }
        }
    }
}
