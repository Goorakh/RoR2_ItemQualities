using HG;
using RoR2;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class RunExtraStatsTracker : NetworkBehaviour
    {
        [SystemInitializer(typeof(GameModeCatalog))]
        private static void Init()
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

        private static RunExtraStatsTracker _instance;
        public static RunExtraStatsTracker Instance => _instance;

        [SyncVar]
        public int AmbientLevelPenalty;

        private void OnEnable()
        {
            SingletonHelper.Assign(ref _instance, this);
        }

        private void OnDisable()
        {
            SingletonHelper.Unassign(ref _instance, this);
        }
    }
}
