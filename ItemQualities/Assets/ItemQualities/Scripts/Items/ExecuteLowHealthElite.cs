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
        public static List<BodyIndex> CanBypassImmunity = new List<BodyIndex>();

        [SystemInitializer(typeof(BodyCatalog))]
        private static void Init()
        {
            ExecuteAPI.CalculateExecuteThresholdForViewerBypassImmunity += calculateExecuteThreshold;

            CanBypassImmunity.Add(BodyCatalog.FindBodyIndex("SolusHeartBody"));
            if (UmbralCompat.Enabled)
            {
                CanBypassImmunity.Add(BodyCatalog.FindBodyIndex("BrotherBody"));
                CanBypassImmunity.Add(BodyCatalog.FindBodyIndex("BrotherHurtBodyP3"));
            }
        }

        private static void calculateExecuteThreshold(CharacterBody victimBody, CharacterBody viewerBody, ref float highestExecuteThreshold)
        {
            if (!victimBody || !viewerBody)
                return;
            if ((victimBody.bodyFlags & CharacterBody.BodyFlags.ImmuneToExecutes) != 0 && !CanBypassImmunity.Contains(victimBody.bodyIndex))
                return;

            if ((victimBody.isBoss || victimBody.isChampion) && viewerBody.TryGetComponentCached(out CharacterBodyExtraStatsTracker viewerBodyExtraStats))
            {
                highestExecuteThreshold = Mathf.Max(highestExecuteThreshold, viewerBodyExtraStats.ExecuteBossHealthFraction);
            }
        }
    }
}
