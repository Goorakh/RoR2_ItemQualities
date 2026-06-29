using RoR2;
using System;

namespace ItemQualities
{
    internal static class SpawnUtils
    {
        public delegate void SceneDirectorEventDelegate(SceneDirector sceneDirector);
        public static event SceneDirectorEventDelegate PreSceneReadyForSpawnsServer;
        public static event SceneDirectorEventDelegate OnSceneReadyForSpawnsServer;

        [InitDuringStartupPhase(GameInitPhase.PostProgressBar)]
        private static void Init()
        {
            // This needs to be a hook in order to run code *after* onPostPopulateSceneServer due to timing issues with how ProperSave loads minions from save data
            On.RoR2.SceneDirector.Start += SceneDirector_Start;
        }

        private static void SceneDirector_Start(On.RoR2.SceneDirector.orig_Start orig, SceneDirector self)
        {
            orig(self);

            if (PreSceneReadyForSpawnsServer != null)
            {
                foreach (SceneDirectorEventDelegate preSceneReadyForSpawnsServer in PreSceneReadyForSpawnsServer.GetInvocationList())
                {
                    try
                    {
                        preSceneReadyForSpawnsServer(self);
                    }
                    catch (Exception e)
                    {
                        Log.Error_NoCallerPrefix(e.ToString());
                    }
                }
            }

            if (OnSceneReadyForSpawnsServer != null)
            {
                foreach (SceneDirectorEventDelegate onSceneReadyForSpawnsServer in OnSceneReadyForSpawnsServer.GetInvocationList())
                {
                    try
                    {
                        onSceneReadyForSpawnsServer(self);
                    }
                    catch (Exception e)
                    {
                        Log.Error_NoCallerPrefix(e.ToString());
                    }
                }
            }
        }
    }
}
