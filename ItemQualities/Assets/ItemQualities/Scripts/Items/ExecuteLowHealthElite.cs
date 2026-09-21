using ItemQualities.ModCompatibility;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    internal static class ExecuteLowHealthElite
    {
        private static BodyIndex _crabJointBodyIndex = BodyIndex.None;

        [SystemInitializer(typeof(BodyCatalog))]
        private static void Init()
        {
            ExecuteAPI.CalculateExecuteThresholdForViewerBypassImmunity += calculateExecuteThreshold;
            _crabJointBodyIndex = BodyCatalog.FindBodyIndex("VoidRaidCrabJointBody");
        }

        private static void calculateExecuteThreshold(CharacterBody victimBody, CharacterBody viewerBody, ref float highestExecuteThreshold)
        {
            if (!victimBody || !viewerBody)
                return;
            if (FathomlessCompat.Enabled && victimBody.bodyIndex == _crabJointBodyIndex)
                return;

            if ((victimBody.isBoss || victimBody.isChampion) && viewerBody.TryGetComponentCached(out CharacterBodyExtraStatsTracker viewerBodyExtraStats))
            {
                highestExecuteThreshold = Mathf.Max(highestExecuteThreshold, viewerBodyExtraStats.ExecuteBossHealthFraction);
            }
        }
    }
}
