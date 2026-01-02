using HG;
using RoR2;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class RunExtraStatsTracker : NetworkBehaviour
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

        static RunExtraStatsTracker _instance;
        public static RunExtraStatsTracker Instance => _instance;

        [SyncVar]
        public int AmbientLevelPenalty;

        void OnEnable()
        {
            SingletonHelper.Assign(ref _instance, this);
        }

        void OnDisable()
        {
            SingletonHelper.Unassign(ref _instance, this);
        }
    }
}
