using ItemQualities.ModCompatibility;
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

        void Awake()
        {
            if (NetworkServer.active)
            {
                BossGroup.onBossGroupDefeatedServer += onBossGroupDefeatedServer;
            }
            On.RoR2.SolusWingGrid.GridManager.OnTierSet += GridManager_OnTierSet;
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
            On.RoR2.SolusWingGrid.GridManager.OnTierSet -= GridManager_OnTierSet;
        }

        private void GridManager_OnTierSet(On.RoR2.SolusWingGrid.GridManager.orig_OnTierSet orig, RoR2.SolusWingGrid.GridManager self, int tier)
        {
            orig(self, tier);
            Vector3 arenacenter = transform.position;
            arenacenter.y = self.GetLavaPosition(tier).y;
            transform.position = arenacenter;

            for (TeamIndex teamIndex = 0; (int)teamIndex < TeamsAPICompat.TeamsCount; teamIndex++)
            {
                GameObject teamHealNovaSpawnerObj = _healNovaSpawnersByTeam[(int)teamIndex];
                if (teamHealNovaSpawnerObj)
                {
                    teamHealNovaSpawnerObj.transform.position = transform.position;
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
                    ItemQualityCounts tpHealingNova = ItemQualitiesContent.ItemQualityGroups.TPHealingNova.GetTeamItemCounts(teamIndex, false);

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
