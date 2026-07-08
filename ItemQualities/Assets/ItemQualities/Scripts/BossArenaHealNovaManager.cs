using HG;
using ItemQualities.ModCompatibility;
using ItemQualities.Utilities;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class BossArenaHealNovaManager : MonoBehaviour
    {
        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.SolusWingGrid.GridManager.OnTierSet += GridManager_OnTierSet;
        }

        private static void GridManager_OnTierSet(On.RoR2.SolusWingGrid.GridManager.orig_OnTierSet orig, RoR2.SolusWingGrid.GridManager self, int tier)
        {
            orig(self, tier);

            try
            {
                foreach (BossArenaHealNovaManager novaManager in InstanceTracker.GetInstancesList<BossArenaHealNovaManager>())
                {
                    Vector3 arenaCenter = novaManager.transform.position;
                    arenaCenter.y = self.GetLavaPosition(tier).y;

                    novaManager.setPosition(arenaCenter);
                }
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e.ToString());
            }
        }

        public BossGroup WatchingBossGroup;

        public float ArenaRadius = 100f;

        private readonly GameObject[] _healNovaSpawnersByTeam = new GameObject[TeamsAPICompat.TeamsCount];

        private void Awake()
        {
            if (!NetworkServer.active)
            {
                Log.Warning("Created on server");
                enabled = false;
                return;
            }
        }

        private void Start()
        {
            if (WatchingBossGroup)
            {
                updateAllTeamHealNovaManagers();
            }
        }

        private void OnEnable()
        {
            InstanceTracker.Add(this);

            if (WatchingBossGroup)
            {
                updateAllTeamHealNovaManagers();
            }

            Inventory.onInventoryChangedGlobal += onInventoryChangedGlobal;

            BossGroup.onBossGroupDefeatedServer += onBossGroupDefeatedServer;
        }

        private void OnDisable()
        {
            InstanceTracker.Remove(this);

            foreach (GameObject healNovaSpawner in _healNovaSpawnersByTeam)
            {
                if (healNovaSpawner)
                {
                    Destroy(healNovaSpawner);
                }
            }

            Inventory.onInventoryChangedGlobal -= onInventoryChangedGlobal;

            BossGroup.onBossGroupDefeatedServer -= onBossGroupDefeatedServer;
        }

        private void onBossGroupDefeatedServer(BossGroup bossGroup)
        {
            if (bossGroup == WatchingBossGroup)
            {
                Destroy(gameObject);
            }
        }

        private void onInventoryChangedGlobal(Inventory inventory)
        {
            if (inventory.TryGetComponent(out CharacterMaster master) && master.teamIndex != TeamIndex.None)
            {
                updateTeamHealNovaManager(master.teamIndex);
            }
        }

        private void updateAllTeamHealNovaManagers()
        {
            for (TeamIndex teamIndex = 0; (int)teamIndex < TeamsAPICompat.TeamsCount; teamIndex++)
            {
                updateTeamHealNovaManager(teamIndex);
            }
        }

        private void updateTeamHealNovaManager(TeamIndex teamIndex)
        {
            if (!ArrayUtils.IsInBounds(_healNovaSpawnersByTeam, (int)teamIndex))
            {
                Log.Warning($"TeamIndex {teamIndex} is not in bounds of heal nova spawners array");
                return;
            }

            ItemQualityCounts tpHealingNova = ItemQualityUtils.GetTeamItemCounts(ItemQualitiesContent.ItemQualityGroups.TPHealingNova, teamIndex, true);

            bool novaSpawnerActive = tpHealingNova.TotalQualityCount > 0;

            ref GameObject teamHealNovaSpawnerObj = ref _healNovaSpawnersByTeam[(int)teamIndex];
            if (teamHealNovaSpawnerObj != novaSpawnerActive)
            {
                if (novaSpawnerActive)
                {
                    teamHealNovaSpawnerObj = Instantiate(ItemQualitiesContent.NetworkedPrefabs.BossArenaHealNovaSpawner, transform.position, transform.rotation);

                    TeamFilter novaSpawnerTeamFilter = teamHealNovaSpawnerObj.GetComponent<TeamFilter>();
                    novaSpawnerTeamFilter.teamIndex = teamIndex;

                    BossGroupHealNovaSpawner teamHealNovaSpawner = teamHealNovaSpawnerObj.GetComponent<BossGroupHealNovaSpawner>();
                    teamHealNovaSpawner.BossGroup = WatchingBossGroup;
                    teamHealNovaSpawner.NovaRadius = ArenaRadius;

                    NetworkServer.Spawn(teamHealNovaSpawnerObj);
                }
                else
                {
                    Destroy(teamHealNovaSpawnerObj);
                    teamHealNovaSpawnerObj = null;
                }
            }
        }

        private void setPosition(Vector3 arenaCenter)
        {
            transform.position = arenaCenter;

            foreach (GameObject healNovaSpawnerObj in _healNovaSpawnersByTeam)
            {
                if (healNovaSpawnerObj && healNovaSpawnerObj.TryGetComponent(out BossGroupHealNovaSpawner healNovaSpawner))
                {
                    healNovaSpawner.SetPositionServer(arenaCenter);
                }
            }

            for (TeamIndex teamIndex = 0; (int)teamIndex < TeamsAPICompat.TeamsCount; teamIndex++)
            {
                GameObject teamHealNovaSpawnerObj = _healNovaSpawnersByTeam[(int)teamIndex];
                if (teamHealNovaSpawnerObj)
                {
                    teamHealNovaSpawnerObj.transform.position = transform.position;
                }
            }
        }
    }
}
