using ItemQualities.ModCompatibility;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using System.Collections.Generic;
using UnityEngine;

namespace ItemQualities.Items
{
    internal static class ExecuteLowHealthElite
    {
        private static HashSet<BodyIndex> bypassExecuteImmunityBodies = new HashSet<BodyIndex>();

        [SystemInitializer(typeof(BodyCatalog))]
        private static void Init()
        {
            ExecuteAPI.CalculateExecuteThresholdForViewerBypassImmunity += calculateExecuteThreshold;

            static void addBypassExecuteImmunityBody(string bodyName)
            {
                BodyIndex bodyIndex = BodyCatalog.FindBodyIndex(bodyName);
                if (bodyIndex != BodyIndex.None)
                {
                    bypassExecuteImmunityBodies.Add(bodyIndex);
                }
            }

            if (UmbralCompat.Enabled)
            {
                addBypassExecuteImmunityBody("BrotherBody");
                addBypassExecuteImmunityBody("BrotherHurtBodyP3");
            }
        }

        private static void calculateExecuteThreshold(CharacterBody victimBody, CharacterBody viewerBody, ref float highestExecuteThreshold)
        {
            if (!victimBody || !viewerBody)
                return;

            if ((victimBody.bodyFlags & CharacterBody.BodyFlags.ImmuneToExecutes) != 0 && !bypassExecuteImmunityBodies.Contains(victimBody.bodyIndex))
                return;

            if ((victimBody.isBoss || victimBody.isChampion) && viewerBody.TryGetComponentCached(out CharacterBodyExtraStatsTracker viewerBodyExtraStats))
            {
                highestExecuteThreshold = Mathf.Max(highestExecuteThreshold, viewerBodyExtraStats.ExecuteBossHealthFraction);
            }
        }
    }
}
