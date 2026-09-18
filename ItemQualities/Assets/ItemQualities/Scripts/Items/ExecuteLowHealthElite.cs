using ItemQualities.ModCompatibility;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;

namespace ItemQualities.Items
{
    internal static class ExecuteLowHealthElite
    {
        private static BodyIndex _crabJointBodyIndex;

        [SystemInitializer]
        private static void Init()
        {
            ExecuteAPI.CalculateExecuteThresholdForViewerBypassImmunity += calculateExecuteThreshold;
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_DLC1_VoidRaidCrab.VoidRaidCrabJointBody_prefab).OnSuccess(crabJointPrefab =>
            {
                if (crabJointPrefab.TryGetComponent(out CharacterBody body))
                {
                    _crabJointBodyIndex = body.bodyIndex;
                }
            });
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
