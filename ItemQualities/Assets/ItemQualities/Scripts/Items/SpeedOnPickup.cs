using HG;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.Navigation;
using RoR2BepInExPack.GameAssetPaths.Version_1_35_0;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    static class SpeedOnPickup
    {
        static InteractableSpawnCard _iscSpeedOnPickupBarrel;

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> barrelLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Barrel1.Barrel1_prefab);
            AsyncOperationHandle<Sprite> barrelIconLoad = AddressableUtil.LoadAssetAsync<Sprite>(RoR2_Base_Common_MiscIcons.texBarrelIcon_png);
            AsyncOperationHandle<GameObject> speedOnPickupSalvageLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC3_Items_SpeedOnPickup.SpeedOnPickupSalvage_prefab);
            AsyncOperationHandle<GameObject> extraStatsOnLevelUpScrapEffectLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC2_Items_ExtraStatsOnLevelUp.ExtraStatsOnLevelUpScrapEffect_prefab);

            ParallelProgressCoroutine parallelLoad = new ParallelProgressCoroutine(args.ProgressReceiver);
            parallelLoad.Add(barrelLoad);
            parallelLoad.Add(barrelIconLoad);
            parallelLoad.Add(speedOnPickupSalvageLoad);
            parallelLoad.Add(extraStatsOnLevelUpScrapEffectLoad);
            yield return parallelLoad;

            GameObject speedOnPickupBarrelSalvagePickupEffectPrefab = null;
            if (extraStatsOnLevelUpScrapEffectLoad.IsValid() && extraStatsOnLevelUpScrapEffectLoad.Status == AsyncOperationStatus.Succeeded && extraStatsOnLevelUpScrapEffectLoad.Result)
            {
                speedOnPickupBarrelSalvagePickupEffectPrefab = extraStatsOnLevelUpScrapEffectLoad.Result.InstantiateClone("SpeedOnPickupBarrelSalvagePickupEffect", false);

                if (speedOnPickupBarrelSalvagePickupEffectPrefab.TryGetComponent(out MultiTextRiserController multiTextRiserController))
                {
                    multiTextRiserController.DisplayStrings = new string[]
                    {
                        "SPEEDONPICKUP_STATBUFF_DAMAGE",
                        "SPEEDONPICKUP_STATBUFF_HEALTH",
                        "SPEEDONPICKUP_STATBUFF_ATTACKSPEED",
                        "SPEEDONPICKUP_STATBUFF_MOVESPEED",
                        "SPEEDONPICKUP_STATBUFF_REGENERATION",
                        "SPEEDONPICKUP_STATBUFF_CRIT",
                        "SPEEDONPICKUP_STATBUFF_ARMOR",
                    };

                    multiTextRiserController.BaseDuration = 1.5f;
                    multiTextRiserController.DurationModifier = 0f;
                }
                else
                {
                    Log.Error($"Missing MultiTextRiserController component on {extraStatsOnLevelUpScrapEffectLoad.Result}");
                }
            }
            else
            {
                Log.Error($"Failed to load beads scrap effect: {(extraStatsOnLevelUpScrapEffectLoad.IsValid() ? extraStatsOnLevelUpScrapEffectLoad.OperationException : "invalid handle")}");
            }

            GameObject speedOnPickupBarrelSalvagePrefab = null;
            if (speedOnPickupSalvageLoad.IsValid() && speedOnPickupSalvageLoad.Status == AsyncOperationStatus.Succeeded && speedOnPickupSalvageLoad.Result)
            {
                speedOnPickupBarrelSalvagePrefab = speedOnPickupSalvageLoad.Result.InstantiateClone("SpeedOnPickupBarrelSalvage");

                HealthPickup healthPickup = speedOnPickupBarrelSalvagePrefab.GetComponentInChildren<HealthPickup>();
                if (healthPickup)
                {
                    SpeedOnPickupStatsPickup speedOnPickupStatsPickup = healthPickup.gameObject.AddComponent<SpeedOnPickupStatsPickup>();
                    speedOnPickupStatsPickup.BaseObject = healthPickup.baseObject;
                    speedOnPickupStatsPickup.TeamFilter = healthPickup.teamFilter;
                    speedOnPickupStatsPickup.PickupEffect = speedOnPickupBarrelSalvagePickupEffectPrefab ? speedOnPickupBarrelSalvagePickupEffectPrefab : healthPickup.pickupEffect;

                    GameObject.Destroy(healthPickup);
                }
                else
                {
                    Log.Error("Failed to find HealthPickup prefab");
                }
            }
            else
            {
                Log.Error($"Failed to load salvage prefab: {(speedOnPickupSalvageLoad.IsValid() ? speedOnPickupSalvageLoad.OperationException : "invalid handle")}");
            }

            GameObject speedOnPickupBarrelPrefab = null;
            if (barrelLoad.IsValid() && barrelLoad.Status == AsyncOperationStatus.Succeeded && barrelLoad.Result)
            {
                speedOnPickupBarrelPrefab = barrelLoad.Result.InstantiateClone("SpeedOnPickupBarrel");

                Renderer modelRenderer = null;
                if (speedOnPickupBarrelPrefab.TryGetComponent(out ModelLocator modelLocator) && modelLocator.modelTransform)
                {
                    modelLocator.modelTransform.name = "mdlSpeedOnPickupBarrel";

                    if (modelLocator.modelTransform.TryGetComponent(out RandomizeSplatBias randomizeSplatBias))
                    {
                        GameObject.Destroy(randomizeSplatBias);
                    }

                    modelRenderer = modelLocator.modelTransform.GetComponentInChildren<Renderer>();
                }

                Texture goldTexture = args.ContentPack.textures.Find("texTrimSheetConstructionGold");
                if (goldTexture)
                {
                    if (modelRenderer)
                    {
                        Material goldMaterial = new Material(modelRenderer.sharedMaterial);
                        goldMaterial.name = "matBarrelGold";
                        goldMaterial.mainTexture = goldTexture;
                        goldMaterial.SetFloat("_Smoothness", 0.9f);
                        goldMaterial.SetFloat("_SpecularStrength", 0.3f);
                        goldMaterial.SetFloat("_SpecularExponent", 2f);
                        goldMaterial.SetInt("_FEON", 1);
                        modelRenderer.sharedMaterial = goldMaterial;
                    }
                    else
                    {
                        Log.Error("Failed to find barrel prefab model renderer");
                    }
                }
                else
                {
                    Log.Error("Failed to get collectors compulsion barrel texture");
                }

                GameObject.Destroy(speedOnPickupBarrelPrefab.GetComponent<BarrelInteraction>());

                GenericDisplayNameProvider genericDisplayNameProvider = speedOnPickupBarrelPrefab.EnsureComponent<GenericDisplayNameProvider>();
                genericDisplayNameProvider.displayToken = "BARREL_SPEEDONPICKUP_NAME";

                SpeedOnPickupBarrelInteraction speedOnPickupBarrelInteraction = speedOnPickupBarrelPrefab.AddComponent<SpeedOnPickupBarrelInteraction>();
                speedOnPickupBarrelInteraction.PickupPrefab = speedOnPickupBarrelSalvagePrefab;

                if (speedOnPickupBarrelPrefab.TryGetComponent(out SpecialObjectAttributes specialObjectAttributes))
                {
                    for (int i = specialObjectAttributes.behavioursToDisable.Count - 1; i >= 0; i--)
                    {
                        if (!specialObjectAttributes.behavioursToDisable[i])
                        {
                            specialObjectAttributes.behavioursToDisable.RemoveAt(i);
                        }
                    }

                    specialObjectAttributes.behavioursToDisable.Add(speedOnPickupBarrelInteraction);
                }

                args.ContentPack.networkedObjectPrefabs.Add(new GameObject[]
                {
                    speedOnPickupBarrelPrefab,
                    speedOnPickupBarrelSalvagePrefab
                });

                args.ContentPack.effectDefs.Add(new EffectDef[]
                {
                    new EffectDef(speedOnPickupBarrelSalvagePickupEffectPrefab)
                });

                _iscSpeedOnPickupBarrel = ScriptableObject.CreateInstance<InteractableSpawnCard>();
                _iscSpeedOnPickupBarrel.name = "iscSpeedOnPickupBarrel";
                _iscSpeedOnPickupBarrel.prefab = speedOnPickupBarrelPrefab;
                _iscSpeedOnPickupBarrel.sendOverNetwork = true;
                _iscSpeedOnPickupBarrel.hullSize = HullClassification.Human;
                _iscSpeedOnPickupBarrel.nodeGraphType = MapNodeGroup.GraphType.Ground;
                _iscSpeedOnPickupBarrel.requiredFlags = NodeFlags.None;
                _iscSpeedOnPickupBarrel.forbiddenFlags = NodeFlags.NoChestSpawn;
                _iscSpeedOnPickupBarrel.occupyPosition = true;
                _iscSpeedOnPickupBarrel.orientToFloor = true;
                _iscSpeedOnPickupBarrel.slightlyRandomizeOrientation = true;

                args.ContentPack.spawnCards.Add(_iscSpeedOnPickupBarrel);
            }
            else
            {
                Log.Error($"Failed to load barrel prefab: {(barrelLoad.IsValid() ? barrelLoad.OperationException : "invalid handle")}");
            }

            if (barrelIconLoad.IsValid() && barrelIconLoad.Status == AsyncOperationStatus.Succeeded && barrelIconLoad.Result)
            {
                if (speedOnPickupBarrelPrefab)
                {
                    PingInfoProvider pingInfoProvider = speedOnPickupBarrelPrefab.EnsureComponent<PingInfoProvider>();
                    if (!pingInfoProvider.pingIconOverride)
                    {
                        pingInfoProvider.pingIconOverride = barrelIconLoad.Result;
                    }
                }
            }
            else
            {
                Log.Error($"Failed to load barrel icon sprite: {(barrelIconLoad.IsValid() ? barrelIconLoad.OperationException : "invalid handle")}");
            }
        }

        [SystemInitializer]
        static void Init()
        {
            SceneDirector.onPostPopulateSceneServer += onPostPopulateSceneServer;

            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
        }

        static void onPostPopulateSceneServer(SceneDirector sceneDirector)
        {
            if (!_iscSpeedOnPickupBarrel)
                return;

            if (SceneInfo.instance.countsAsStage || SceneInfo.instance.sceneDef.allowItemsToSpawnObjects)
            {
                Xoroshiro128Plus rng = new Xoroshiro128Plus(sceneDirector.rng.nextUlong);

                foreach (CharacterMaster master in CharacterMaster.readOnlyInstancesList)
                {
                    if (master.inventory)
                    {
                        ItemQualityCounts speedOnPickup = master.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SpeedOnPickup);
                        if (speedOnPickup.TotalQualityCount > 0)
                        {
                            DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(_iscSpeedOnPickupBarrel, new DirectorPlacementRule
                            {
                                placementMode = SceneInfo.instance.approximateMapBoundMesh ? DirectorPlacementRule.PlacementMode.RandomNormalized : DirectorPlacementRule.PlacementMode.Random,
                            }, rng));
                        }
                    }
                }
            }
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender.master && sender.master.TryGetComponent(out CharacterMasterExtraStatsTracker masterExtraStats) && masterExtraStats.SpeedOnPickupBonus > 0)
            {
                float multiplier = 1f + (0.01f * masterExtraStats.SpeedOnPickupBonus);
                if (multiplier > 0)
                {
                    args.armorTotalMult *= multiplier;
                    args.attackSpeedTotalMult *= multiplier;
                    args.critDamageTotalMult *= multiplier;
                    args.critTotalMult *= multiplier;
                    args.damageTotalMult *= multiplier;
                    args.healthTotalMult *= multiplier;
                    args.moveSpeedTotalMult *= multiplier;
                    args.regenTotalMult *= multiplier;
                }
            }
        }
    }
}
