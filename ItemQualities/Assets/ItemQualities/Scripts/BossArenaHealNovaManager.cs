using ItemQualities.ModCompatibility;
using ItemQualities.Utilities;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public class BossArenaHealNovaManager : MonoBehaviour
    {
        public BossGroup WatchingBossGroup;

        public float ArenaRadius = 100f;

        readonly GameObject[] _healNovaSpawnersByTeam = new GameObject[TeamsAPICompat.TeamsCount];

        [SystemInitializer()]
        static void Init()
        {
            On.RoR2.SolusWingGrid.GridManager.OnTierSet += GridManager_OnTierSet;
        }

        void Awake()
        {
            InstanceTracker.Add(this);
            if (NetworkServer.active)
            {
                BossGroup.onBossGroupDefeatedServer += onBossGroupDefeatedServer;
            }
        }

        void OnDisable()
        {
            foreach (GameObject healNovaSpawner in _healNovaSpawnersByTeam)
            {
                if (healNovaSpawner)
                {
                    Destroy(healNovaSpawner);
                }
            }
        }

        void OnDestroy()
        {
            BossGroup.onBossGroupDefeatedServer -= onBossGroupDefeatedServer;
        }

        private static void GridManager_OnTierSet(On.RoR2.SolusWingGrid.GridManager.orig_OnTierSet orig, RoR2.SolusWingGrid.GridManager self, int tier)
        {
            orig(self, tier);
            foreach (BossArenaHealNovaManager novaManager in InstanceTracker.GetInstancesList<BossArenaHealNovaManager>())
            {
                Vector3 arenacenter = novaManager.transform.position;
                arenacenter.y = self.GetLavaPosition(tier).y;
                novaManager.transform.position = arenacenter;

                for (TeamIndex teamIndex = 0; (int)teamIndex < TeamsAPICompat.TeamsCount; teamIndex++)
                {
                    GameObject teamHealNovaSpawnerObj = novaManager._healNovaSpawnersByTeam[(int)teamIndex];
                    if (teamHealNovaSpawnerObj)
                    {
                        teamHealNovaSpawnerObj.transform.position = novaManager.transform.position;
                    }
                }
            }
        }

        void onBossGroupDefeatedServer(BossGroup bossGroup)
        {
            if (bossGroup == WatchingBossGroup)
            {
                Destroy(gameObject);
            }
        }

        void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                for (TeamIndex teamIndex = 0; (int)teamIndex < TeamsAPICompat.TeamsCount; teamIndex++)
                {
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
            }
        }
    }
}
