using HG;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using R2API;
using RoR2;
using RoR2.Navigation;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    internal static class TitanGoldDuringTP
    {
        private static readonly float _thrownDuplicatorForwardSpeed = 10f;
        private static readonly float _thrownDuplicatorVerticalSpeed = 45f;

        private static GameObject _thrownObjectProjectilePrefab;

        private static SceneIndex _solutationalHauntSceneIndex = SceneIndex.Invalid;

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> thrownObjectProjectileLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC3_Drifter.ThrownObjectProjectileNoStun_prefab);
            thrownObjectProjectileLoad.OnSuccess(thrownObjectProjectilePrefab =>
            {
                _thrownObjectProjectilePrefab = thrownObjectProjectilePrefab.InstantiateClone("ThrownObjectProjectile_QualityDuplicatorWild");

                if (_thrownObjectProjectilePrefab.TryGetComponent(out VehicleSeat vehicleSeat))
                {
                    vehicleSeat.inheritRotation = true;
                }

                args.ContentPack.projectilePrefabs.Add(_thrownObjectProjectilePrefab);
            });

            return thrownObjectProjectileLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer(typeof(SceneCatalog))]
        private static void Init()
        {
            _solutationalHauntSceneIndex = SceneCatalog.FindSceneIndex("solutionalhaunt");
            if (_solutationalHauntSceneIndex == SceneIndex.Invalid)
            {
                Log.Warning("Failed to find solutationalhaunt scene index");
            }

            BossGroup.onBossGroupDefeatedServer += onBossGroupDefeatedServer;

            On.EntityStates.SolusHeart.Death.SolusHeartFinaleSequence.Death.OnEnter += SolusHeartFinaleDeath_OnEnter;
            On.EntityStates.SolusWing.PostFight.SpawnEyePortal.OnEnter += SolusWithPostFightSpawnEyePortal_OnEnter;

            On.RoR2.GoldTitanManager.GoldTitanItemFilter += GoldTitanManager_GoldTitanItemFilter;

            MethodInfo tryStartChannelingTitansServerMethod = typeof(GoldTitanManager).GetMethod(nameof(GoldTitanManager.TryStartChannelingTitansServer), ReflectionUtil.AllFlags);
            if (tryStartChannelingTitansServerMethod != null)
            {
                using DynamicMethodDefinition dmd = new DynamicMethodDefinition(tryStartChannelingTitansServerMethod);
                using ILContext il = new ILContext(dmd.Definition);

                ILCursor c = new ILCursor(il);

                MethodReference onSpawnedServerMethodRef = null;
                if (c.TryGotoNext(x => x.MatchLdftn(out onSpawnedServerMethodRef),
                                  x => x.MatchNewobj<Action<SpawnCard.SpawnResult>>(),
                                  x => x.MatchStfld<DirectorSpawnRequest>(nameof(DirectorSpawnRequest.onSpawnedServer))))
                {
                    MethodBase onSpawnedServerMethod = null;
                    try
                    {
                        onSpawnedServerMethod = onSpawnedServerMethodRef.ResolveReflection();
                    }
                    catch (Exception e)
                    {
                        Log.Error_NoCallerPrefix($"Failed to resolve GoldTitanManager.TryStartChannelingTitansServer onSpawnedServer method: {e}");
                    }

                    if (onSpawnedServerMethod != null)
                    {
                        var hook = new Hook(onSpawnedServerMethod, new Action<Action<object, SpawnCard.SpawnResult>, object, SpawnCard.SpawnResult>(GoldTitanManager_TryStartChannelingTitansServer_onSpawnedServer), new HookConfig
                        {
                            ManualApply = true
                        });

                        try
                        {
                            hook.Apply();
                        }
                        catch (Exception e)
                        {
                            Log.Error_NoCallerPrefix($"Failed to apply GoldTitanManager.TryStartChannelingTitansServer onSpawnedServer hook: {e}");
                            hook?.Dispose();
                        }
                    }
                }
                else
                {
                    Log.Error("Failed to find onSpawnedServer method in GoldTitanManager.TryStartChannelingTitansServer");
                }
            }
            else
            {
                Log.Error("Failed to find GoldTitanManager.TryStartChannelingTitansServer method");
            }
        }

        private static void attemptSpawnPrinterRewardServer(Vector3 rewardPosition, Xoroshiro128Plus rng, bool aimAtNearbyNodes = true)
        {
            if (!NetworkServer.active)
            {
                Log.Warning("Called on client");
                return;
            }

            ItemQualityCounts titanGoldDuringTP = new ItemQualityCounts();
            foreach (CharacterMaster master in CharacterMaster.readOnlyInstancesList)
            {
                if (master.hasBody && !master.IsDeadAndOutOfLivesServer())
                {
                    titanGoldDuringTP += master.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.TitanGoldDuringTP);
                }
            }

            foreach (TitanQualityController titanQualityController in InstanceTracker.GetInstancesList<TitanQualityController>())
            {
                titanGoldDuringTP += titanQualityController.TitanQualityCounts;
            }

            if (titanGoldDuringTP.TotalQualityCount > 0)
            {
                Vector3 rewardPositionXZ = rewardPosition;
                rewardPositionXZ.y = 0f;

                Vector3? targetNodePosition = null;
                if (aimAtNearbyNodes)
                {
                    NodeGraph groundNodeGraph = SceneInfo.instance ? SceneInfo.instance.groundNodes : null;
                    if (groundNodeGraph)
                    {
                        using var _ = ListPool<NodeGraph.NodeIndex>.RentCollection(out List<NodeGraph.NodeIndex> nodes);
                        groundNodeGraph.FindNodesInRangeWithFlagConditions(rewardPosition, 25f, 60f, HullMask.Human, NodeFlags.None, NodeFlags.NoChestSpawn, false, nodes);

                        while (!targetNodePosition.HasValue && nodes.Count > 0)
                        {
                            NodeGraph.NodeIndex nodeIndex = ListUtils.Take(nodes, rng.RangeInt(0, nodes.Count));
                            if (!groundNodeGraph.GetNodePosition(nodeIndex, out Vector3 nodePosition))
                                continue;

                            if (nodePosition.y > rewardPosition.y + 10f)
                                continue;

                            Vector3 nodePositionXZ = nodePosition;
                            nodePositionXZ.y = 0f;

                            // Some boss reward spots can have nodes that are technically far enough away, but horizontally theyre basically right on top of the spawn point, so filter these out
                            const float MinXZDistance = 15f;
                            float xzDistanceSqr = (nodePositionXZ - rewardPositionXZ).sqrMagnitude;
                            if (xzDistanceSqr <= MinXZDistance * MinXZDistance)
                                continue;

                            targetNodePosition = nodePosition;
                            break;
                        }
                    }
                }

                Vector3 dropVelocity;
                if (targetNodePosition.HasValue)
                {
                    Log.Debug($"Target node position: {targetNodePosition.Value}");

                    dropVelocity = Trajectory.CalculateInitialVelocityFromHSpeed(rewardPosition, targetNodePosition.Value, _thrownDuplicatorForwardSpeed);
                }
                else
                {
                    if (aimAtNearbyNodes)
                    {
                        Log.Debug("Failed to find target node position");
                    }

                    dropVelocity = Quaternion.AngleAxis(rng.RangeFloat(0f, 360f), Vector3.up) * ((Vector3.up * _thrownDuplicatorVerticalSpeed) + (Vector3.forward * _thrownDuplicatorForwardSpeed));
                }

                Quaternion dropRotation = Quaternion.LookRotation(dropVelocity, Vector3.up);

                Log.Debug($"Spawning duplicator projectile at {rewardPosition}");

                GameObject duplicatorObject = GameObject.Instantiate(ItemQualitiesContent.SpawnCards.QualityDuplicatorWild.prefab, rewardPosition, Quaternion.Euler(0f, dropRotation.eulerAngles.y, 0f));
                NetworkServer.Spawn(duplicatorObject);

                GameObject thrownObjectProjectile = ProjectileManager.instance.FireProjectileImmediateServer(new FireProjectileInfo
                {
                    projectilePrefab = _thrownObjectProjectilePrefab,
                    position = rewardPosition,
                    rotation = dropRotation,
                    damage = 0f,
                    speedOverride = dropVelocity.magnitude,
                });

                if (thrownObjectProjectile.TryGetComponent(out ThrownObjectProjectileController thrownObjectProjectileController))
                {
                    thrownObjectProjectileController.SetPassengerServer(duplicatorObject);
                }
            }
        }

        private static void onBossGroupDefeatedServer(BossGroup bossGroup)
        {
            // Don't spawn for teleporters
            if (bossGroup.GetComponent<TeleporterInteraction>())
                return;

            // Spawn is handled manually for solus heart
            if (bossGroup.GetComponent<SolusWebMissionController>())
                return;

            SceneDef currentSceneDef = SceneInfo.instance ? SceneInfo.instance.sceneDef : null;
            SceneIndex currentSceneIndex = currentSceneDef ? currentSceneDef.sceneDefIndex : SceneIndex.Invalid;
            if (currentSceneIndex != SceneIndex.Invalid)
            {
                // Spawn is handled manually for solus wing
                if (currentSceneIndex == _solutationalHauntSceneIndex && bossGroup.name == "Boss Group CombatSquad")
                    return;
            }

            // Ignore all boss groups that aren't the final phase
            if (BossUtil.IsNonFinalPhase(bossGroup))
            {
                Log.Debug($"Non-final phase BossGroup {Util.GetGameObjectHierarchyName(bossGroup.gameObject)} defeated, ignoring");
                return;
            }

            Vector3 rewardPosition = BossUtil.GetBestRewardPosition(bossGroup) + new Vector3(0f, 1f, 0f);

            Xoroshiro128Plus rng = new Xoroshiro128Plus(bossGroup.rng.nextUlong);

            attemptSpawnPrinterRewardServer(rewardPosition, rng);
        }

        private static void SolusHeartFinaleDeath_OnEnter(On.EntityStates.SolusHeart.Death.SolusHeartFinaleSequence.Death.orig_OnEnter orig, EntityStates.SolusHeart.Death.SolusHeartFinaleSequence.Death self)
        {
            orig(self);

            if (NetworkServer.active)
            {
                attemptSpawnPrinterRewardServer(self.characterBody.corePosition, new Xoroshiro128Plus(Run.instance.spawnRng.nextUlong), false);
            }
        }

        private static void SolusWithPostFightSpawnEyePortal_OnEnter(On.EntityStates.SolusWing.PostFight.SpawnEyePortal.orig_OnEnter orig, EntityStates.SolusWing.PostFight.SpawnEyePortal self)
        {
            orig(self);

            if (NetworkServer.active)
            {
                Transform pickupSpawnPosition = self.FindModelChild("PickupTarget");
                if (pickupSpawnPosition)
                {
                    attemptSpawnPrinterRewardServer(pickupSpawnPosition.transform.position + new Vector3(0f, 3f, 0f), new Xoroshiro128Plus(Run.instance.spawnRng.nextUlong), false);
                }
                else
                {
                    Log.Error("Failed to find reward spawn position");
                }
            }
        }

        private static bool GoldTitanManager_GoldTitanItemFilter(On.RoR2.GoldTitanManager.orig_GoldTitanItemFilter orig, ItemIndex itemIndex)
        {
            return orig(QualityCatalog.GetItemIndexOfQuality(itemIndex, QualityTier.None));
        }

        private static void GoldTitanManager_TryStartChannelingTitansServer_onSpawnedServer(Action<object, SpawnCard.SpawnResult> orig, object self, SpawnCard.SpawnResult spawnResult)
        {
            orig(self, spawnResult);

            if (spawnResult.success && spawnResult.spawnedInstance)
            {
                spawnResult.spawnedInstance.EnsureComponent<TitanQualityController>();
            }
        }

        private sealed class TitanQualityController : MonoBehaviour
        {
            private ItemStealController _itemStealController;
            private CharacterMaster _master;
            private Inventory _inventory;

            public ItemQualityCounts TitanQualityCounts;

            private void Awake()
            {
                _itemStealController = GetComponent<ItemStealController>();
                _master = GetComponent<CharacterMaster>();
                _inventory = GetComponent<Inventory>();

                if (!_itemStealController || !_inventory)
                {
                    Log.Error($"Missing component! has ItemStealController={_itemStealController != null}, Inventory={_inventory != null}");
                    return;
                }
            }

            private void OnEnable()
            {
                InstanceTracker.Add(this);

                if (_itemStealController)
                {
                    _itemStealController.onStealFinishServer ??= new UnityEvent();
                    _itemStealController.onStealFinishServer.AddListener(onStealFinishServer);
                }

                refreshQualityTier();
            }

            private void OnDisable()
            {
                if (_itemStealController)
                {
                    _itemStealController.onStealFinishServer?.RemoveListener(onStealFinishServer);
                }

                InstanceTracker.Remove(this);
            }

            private void onStealFinishServer()
            {
                refreshQualityTier();
            }

            private void refreshQualityTier()
            {
                ItemQualityCounts titanQualityCounts = new ItemQualityCounts();

                if (_itemStealController)
                {
                    foreach (ItemStealController.StolenInventoryInfo stolenInventoryInfo in _itemStealController.stolenInventoryInfos)
                    {
                        for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                        {
                            ItemIndex stolenItemIndex = ItemQualitiesContent.ItemQualityGroups.TitanGoldDuringTP.GetItemIndex(qualityTier);

                            int stolenCount = ArrayUtils.GetSafe(stolenInventoryInfo.stolenItemStacks, (int)stolenItemIndex) +
                                              Mathf.CeilToInt(ArrayUtils.GetSafe(stolenInventoryInfo.stolenTempItemStacks, (int)stolenItemIndex));

                            titanQualityCounts[qualityTier] += stolenCount;
                        }
                    }
                }

                setTitanQualityCounts(titanQualityCounts);
            }

            private void setTitanQualityCounts(ItemQualityCounts newTitanQualityCounts)
            {
                if (TitanQualityCounts == newTitanQualityCounts)
                    return;

                QualityTier prevTitanQualityTier = TitanQualityCounts.HighestQuality;
                QualityTier newTitanQualityTier = newTitanQualityCounts.HighestQuality;

                int prevAttackSpeedBonus = getTitanAttackSpeedBonus(prevTitanQualityTier);
                int newAttackSpeedBonus = getTitanAttackSpeedBonus(newTitanQualityTier);

                TitanQualityCounts = newTitanQualityCounts;

                // kinda a hack to prevent enemy auri (ex from false son) from getting stronger by your item being higher quality
                bool allowStatBoosts = _master && _master.teamIndex == TeamIndex.Player;
                if (!allowStatBoosts)
                {
                    newAttackSpeedBonus = 0;
                }

                if (prevTitanQualityTier != newTitanQualityTier)
                {
                    if (_inventory)
                    {
                        if (prevTitanQualityTier != QualityTier.None)
                        {
                            _inventory.RemoveItemChanneled(ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(prevTitanQualityTier));
                        }

                        if (allowStatBoosts && newTitanQualityTier != QualityTier.None)
                        {
                            _inventory.GiveItemChanneled(ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(newTitanQualityTier));
                        }
                    }
                }

                int attackSpeedBonusDiff = newAttackSpeedBonus - prevAttackSpeedBonus;
                if (attackSpeedBonusDiff != 0)
                {
                    if (_inventory)
                    {
                        _inventory.GiveItemChanneled(RoR2Content.Items.BoostAttackSpeed.itemIndex, attackSpeedBonusDiff);
                    }
                }
            }

            private static int getTitanAttackSpeedBonus(QualityTier qualityTier)
            {
                switch (qualityTier)
                {
                    case QualityTier.None:
                        return 0;
                    case QualityTier.Uncommon:
                        return 2; // +20%
                    case QualityTier.Rare:
                        return 5; // +50%
                    case QualityTier.Epic:
                        return 10; // +100%
                    case QualityTier.Legendary:
                        return 25; // +250%
                    default:
                        Log.Warning_NoCallerPrefix($"Quality tier {qualityTier} is not implemented");
                        return 0;
                }
            }
        }
    }
}
