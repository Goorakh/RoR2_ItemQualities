using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    static class SprintArmor
    {
        public static GameObject BucklerDefenseBigPrefab;

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> bucklerDefenseLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_SprintArmor.BucklerDefense_prefab);
            bucklerDefenseLoad.OnSuccess(bucklerDefense =>
            {
                BucklerDefenseBigPrefab = bucklerDefense.InstantiateClone("BucklerDefenseBig", false);

                RotateObject rotateObject = BucklerDefenseBigPrefab.GetComponentInChildren<RotateObject>(true);
                if (rotateObject)
                {
                    rotateObject.rotationSpeed *= 0.5f;
                }
            });

            return bucklerDefenseLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender)
                return;

            ItemQualityCounts sprintArmor = ItemQualitiesContent.ItemQualityGroups.SprintArmor.GetItemCounts(sender.inventory);

            if (sender.HasBuff(ItemQualitiesContent.Buffs.SprintArmorStrong) && sprintArmor.TotalQualityCount > 0)
            {
                args.armorAdd += (30 * sprintArmor.UncommonCount) +
                                 (50 * sprintArmor.RareCount) +
                                 (75 * sprintArmor.EpicCount) +
                                 (100 * sprintArmor.LegendaryCount);
            }
        }
    }
}
