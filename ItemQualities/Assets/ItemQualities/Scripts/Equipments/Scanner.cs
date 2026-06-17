using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.Navigation;
using RoR2.UI;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Equipments
{
    static class Scanner
    {
        static InteractableSpawnCard _iscChest1Stealthed;

        [ContentInitializer]
        static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> chest2LoadHandle = AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_Chest2.Chest2_prefab);
            AsyncOperationHandle<Material> cloakedMaterialLoadHandle = AddressableUtil.LoadAssetAsync<Material>(RoR2_Base_Common.matCloakedEffect_mat);

            ParallelProgressCoroutine coroutine = new ParallelProgressCoroutine(args.ProgressReceiver);
            coroutine.Add(chest2LoadHandle);
            coroutine.Add(cloakedMaterialLoadHandle);

            yield return coroutine;

            if (!chest2LoadHandle.AssertLoaded("Chest2") || !cloakedMaterialLoadHandle.AssertLoaded("matCloakedEffect"))
                yield break;

            GameObject chest2CloakedPrefab = chest2LoadHandle.Result.InstantiateClone("Chest2StealthedVariant");
            
            if (chest2CloakedPrefab.TryGetComponent(out PurchaseInteraction purchaseInteraction))
            {
                purchaseInteraction.costType = CostTypeIndex.None;
                purchaseInteraction.cost = 0;

                purchaseInteraction.displayNameToken = "QUALITY_CHEST2_STEALTHED_NAME";
                purchaseInteraction.contextToken = "QUALITY_CHEST2_STEALTHED_CONTEXT";
            }
            else
            {
                Log.Warning($"Expected component PurchaseInteraction on {chest2CloakedPrefab}");
            }

            if (chest2CloakedPrefab.TryGetComponent(out ModelLocator modelLocator) && modelLocator._modelTransform)
            {
                foreach (SkinnedMeshRenderer renderer in modelLocator._modelTransform.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    renderer.sharedMaterial = cloakedMaterialLoadHandle.Result;
                }
            }
            else
            {
                Log.Warning($"Failed to find {chest2CloakedPrefab} model transform");
            }

            if (chest2CloakedPrefab.TryGetComponent(out GenericDisplayNameProvider genericDisplayNameProvider))
            {
                genericDisplayNameProvider.displayToken = "QUALITY_CHEST2_STEALTHED_NAME";
            }

            if (chest2CloakedPrefab.TryGetComponent(out GenericInspectInfoProvider genericInspectInfoProvider))
            {
                InspectDef inspectDef = ScriptableObject.Instantiate(genericInspectInfoProvider.InspectInfo);
                inspectDef.name = "CloakedChest2InspectDef";
                inspectDef.Info = new InspectInfo
                {
                    TitleToken = "QUALITY_CHEST2_STEALTHED_NAME",
                    DescriptionToken = "QUALITY_CHEST2_STEALTHED_DESCRIPTION",
                };

                genericInspectInfoProvider.InspectInfo = inspectDef;
            }

            InteractableSpawnCard iscChest2Stealthed = InteractableSpawnCard.CreateInstance<InteractableSpawnCard>();
            iscChest2Stealthed.name = $"isc{nameof(ItemQualitiesContent.SpawnCards.Chest2Stealthed)}";
            iscChest2Stealthed.prefab = chest2CloakedPrefab;
            iscChest2Stealthed.sendOverNetwork = true;
            iscChest2Stealthed.hullSize = HullClassification.Human;
            iscChest2Stealthed.nodeGraphType = MapNodeGroup.GraphType.Ground;
            iscChest2Stealthed.requiredFlags = NodeFlags.None;
            iscChest2Stealthed.forbiddenFlags = NodeFlags.NoChestSpawn;
            iscChest2Stealthed.occupyPosition = true;
            iscChest2Stealthed.orientToFloor = true;
            iscChest2Stealthed.slightlyRandomizeOrientation = true;

            args.ContentPack.spawnCards.Add(iscChest2Stealthed);
            args.ContentPack.networkedObjectPrefabs.Add(chest2CloakedPrefab);
        }

        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<InteractableSpawnCard>(RoR2_Base_Chest1StealthedVariant.iscChest1Stealthed_asset).OnSuccess(cloakedChestSpawnCard =>
            {
                _iscChest1Stealthed = cloakedChestSpawnCard;
            });

            if (ItemQualitiesContent.SpawnCards.QualityChest2 &&
                ItemQualitiesContent.SpawnCards.QualityChest2.prefab &&
                ItemQualitiesContent.SpawnCards.QualityChest2.prefab.TryGetComponent(out ChestBehavior qualityChest2Behavior) &&
                qualityChest2Behavior.dropTable)
            {
                if (ItemQualitiesContent.SpawnCards.Chest2Stealthed &&
                    ItemQualitiesContent.SpawnCards.Chest2Stealthed.prefab &&
                    ItemQualitiesContent.SpawnCards.Chest2Stealthed.prefab.TryGetComponent(out ChestBehavior cloakedChest2Behavior))
                {
                    cloakedChest2Behavior.dropTable = qualityChest2Behavior.dropTable;
                }
            }

            SpawnUtils.OnSceneReadyForSpawnsServer += onSceneReadyForSpawnsServer;
        }

        static void onSceneReadyForSpawnsServer(SceneDirector sceneDirector)
        {
            if (SceneInfo.instance.countsAsStage || SceneInfo.instance.sceneDef.allowItemsToSpawnObjects)
            {
                Xoroshiro128Plus rng = new Xoroshiro128Plus(sceneDirector.rng.nextUlong);

                foreach (CharacterMaster master in CharacterMaster.readOnlyInstancesList)
                {
                    if (master.inventory && !master.inventory.GetEquipmentDisabled())
                    {
                        int equipmentSlotCount = master.inventory.GetEquipmentSlotCount();
                        for (uint slot = 0; slot < equipmentSlotCount; slot++)
                        {
                            int equipmentSetCount = master.inventory.GetEquipmentSetCount(slot);
                            for (uint set = 0; set < equipmentSetCount; set++)
                            {
                                EquipmentState equipmentState = master.inventory.GetEquipment(slot, set);

                                QualityTier equipmentQualityTier = QualityCatalog.GetQualityTier(equipmentState.equipmentIndex);
                                EquipmentQualityGroupIndex equipmentGroupIndex = QualityCatalog.FindEquipmentQualityGroupIndex(equipmentState.equipmentIndex);

                                if (equipmentQualityTier > QualityTier.None && equipmentGroupIndex == ItemQualitiesContent.EquipmentQualityGroups.Scanner.GroupIndex)
                                {
                                    InteractableSpawnCard spawnCard = _iscChest1Stealthed;

                                    // Uncommon: 1
                                    // Rare: 2
                                    // Epic: 3
                                    // Legendary: 4
                                    int spawnCount = (int)equipmentQualityTier + 1;

                                    if (equipmentQualityTier > QualityTier.Rare && ItemQualitiesContent.SpawnCards.Chest2Stealthed)
                                    {
                                        spawnCard = ItemQualitiesContent.SpawnCards.Chest2Stealthed;

                                        // Epic: 1
                                        // Legendary: 2
                                        spawnCount = equipmentQualityTier - QualityTier.Rare;
                                    }

                                    if (spawnCard)
                                    {
                                        DirectorPlacementRule placementRule = new DirectorPlacementRule
                                        {
                                            placementMode = SceneInfo.instance.approximateMapBoundMesh ? DirectorPlacementRule.PlacementMode.RandomNormalized : DirectorPlacementRule.PlacementMode.Random,
                                        };

                                        for (int i = 0; i < spawnCount; i++)
                                        {
                                            DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(spawnCard, placementRule, rng));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
