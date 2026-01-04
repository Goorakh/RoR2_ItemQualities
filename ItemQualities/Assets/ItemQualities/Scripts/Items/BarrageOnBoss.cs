using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    static class BarrageOnBoss
    {
        static SpawnCard _shrineBossPrefab;

        [SystemInitializer]
        static void Init()
        {
            On.RoR2.Run.OnServerTeleporterPlaced += Run_OnServerTeleporterPlaced;
        }

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<SpawnCard> shrineBossLoad = AddressableUtil.LoadTempAssetAsync<SpawnCard>(RoR2_Base_ShrineBoss.iscShrineBoss_asset);

            ParallelProgressCoroutine prefabsLoadCoroutine = new ParallelProgressCoroutine(args.ProgressReceiver);
            prefabsLoadCoroutine.Add(shrineBossLoad);

            yield return prefabsLoadCoroutine;

            if (shrineBossLoad.Status != AsyncOperationStatus.Succeeded || !shrineBossLoad.Result)
            {
                Log.Error($"Failed to load mountain shrine prefab: {shrineBossLoad.OperationException}");
                yield break;
            }

            _shrineBossPrefab = shrineBossLoad.Result;
        }

        static void Run_OnServerTeleporterPlaced(On.RoR2.Run.orig_OnServerTeleporterPlaced orig, Run self, SceneDirector sceneDirector, GameObject teleporter)
        {
            Xoroshiro128Plus xoroshiro128Plus = new Xoroshiro128Plus(RoR2Application.rng.nextUlong);
            foreach (CharacterMaster characterMaster in CharacterMaster.readOnlyInstancesList)
            {
                ItemQualityCounts barrageOnBoss = characterMaster.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BarrageOnBoss);
                if (barrageOnBoss.TotalQualityCount == 0)
                    continue;

                DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(_shrineBossPrefab, new DirectorPlacementRule
                {
                    placementMode = DirectorPlacementRule.PlacementMode.Random
                }, xoroshiro128Plus));
            }
        }
    }
}
