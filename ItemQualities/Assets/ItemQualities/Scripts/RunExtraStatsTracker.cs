using HG;
using RoR2;
using UnityEngine.Networking;

namespace ItemQualities
{
    public class RunExtraStatsTracker : NetworkBehaviour
    {
        [SystemInitializer(typeof(GameModeCatalog))]
        static void Init()
        {
            for (GameModeIndex gameModeIndex = 0; (int)gameModeIndex < GameModeCatalog.gameModeCount; gameModeIndex++)
            {
                Run gameModeRunPrefab = GameModeCatalog.GetGameModePrefabComponent(gameModeIndex);
                if (gameModeRunPrefab)
                {
                    gameModeRunPrefab.gameObject.EnsureComponent<RunExtraStatsTracker>();
                }
            }
        }

        [SyncVar]
        public int AmbientLevelPenalty;
    }
}
