using R2API;
using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    static class ExecuteLowHealthElite
    {
        [SystemInitializer]
        static void Init()
        {
            ExecuteAPI.CalculateExecuteThresholdForViewer += calculateExecuteThreshold;
        }

        static void calculateExecuteThreshold(CharacterBody victimBody, CharacterBody viewerBody, ref float highestExecuteThreshold)
        {
            if (!victimBody || !viewerBody)
                return;

            if ((victimBody.isBoss || victimBody.isChampion) && viewerBody.TryGetComponent(out CharacterBodyExtraStatsTracker viewerBodyExtraStats))
            {
                highestExecuteThreshold = Mathf.Max(highestExecuteThreshold, viewerBodyExtraStats.ExecuteBossHealthFraction);
            }
        }
    }
}
