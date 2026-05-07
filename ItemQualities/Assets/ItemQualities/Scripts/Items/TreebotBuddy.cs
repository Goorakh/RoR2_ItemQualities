using HG;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.Navigation;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    internal static class TreebotBuddy
    {
        private static readonly DeployableSlot[] _treebotBuddyDeployableSlots = new DeployableSlot[(int)QualityTier.Count];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DeployableSlot GetTreebotBuddyDeployableSlot(QualityTier qualityTier)
        {
            return ArrayUtils.GetSafe(_treebotBuddyDeployableSlots, (int)qualityTier, DeployableSlot.None);
        }

        private static DeployableAPI.GetDeployableSameSlotLimit getTreebotBuddyLimitGetter(QualityTier qualityTier)
        {
            int getTreebotBuddyLimit(CharacterMaster master, int swarmsMultiplier)
            {
                return master.inventory.GetItemCountEffective(ItemQualitiesContent.ItemQualityGroups.TreebotBuddy.GetItemIndex(qualityTier));
            }

            return getTreebotBuddyLimit;
        }

        [SystemInitializer]
        private static void Init()
        {
            for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
            {
                _treebotBuddyDeployableSlots[(int)qualityTier] = DeployableAPI.RegisterDeployableSlot(getTreebotBuddyLimitGetter(qualityTier));
            }
        }
    }

    public sealed class TreebotBuddyQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.TreebotBuddy;

        private static GameObject _spawnPodPrefab;

        private static GameObject _treebotAiMasterPrefab;

        private static CharacterSpawnCard _treebotAiSpawnCard;

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            ParallelProgressCoroutine coroutine = new ParallelProgressCoroutine(args.ProgressReceiver);

            AsyncOperationHandle<GameObject> treebotMonsterMasterLoad = AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_Treebot.TreebotMonsterMaster_prefab);
            treebotMonsterMasterLoad.OnSuccess(treebotMonsterMasterPrefab =>
            {
                _treebotAiMasterPrefab = treebotMonsterMasterPrefab.InstantiateClone(treebotMonsterMasterPrefab.name + "_UnlockableSpawn");
                Deployable deployable = _treebotAiMasterPrefab.EnsureComponent<Deployable>();
                deployable.onUndeploy ??= new UnityEvent();

                _treebotAiMasterPrefab.EnsureComponent<SetDontDestroyOnLoad>();

                args.ContentPack.masterPrefabs.Add(_treebotAiMasterPrefab);

                if (!_treebotAiMasterPrefab.TryGetComponent(out CharacterMaster treebotAiMaster))
                {
                    Log.Error($"{_treebotAiMasterPrefab} is missing CharacterMaster component");
                    return;
                }

                deployable.onUndeploy.AddPersistentListener(treebotAiMaster.TrueKill);

                _treebotAiSpawnCard = SpawnCard.CreateInstance<CharacterSpawnCard>();
                _treebotAiSpawnCard.prefab = _treebotAiMasterPrefab;
                _treebotAiSpawnCard.sendOverNetwork = false;
                _treebotAiSpawnCard.hullSize = HullClassification.Human;
                _treebotAiSpawnCard.nodeGraphType = MapNodeGroup.GraphType.Ground;
                _treebotAiSpawnCard.requiredFlags = NodeFlags.None;
                _treebotAiSpawnCard.forbiddenFlags = NodeFlags.NoCharacterSpawn;
                _treebotAiSpawnCard.directorCreditCost = 0;
                _treebotAiSpawnCard.occupyPosition = false;
                
                if (treebotAiMaster.bodyPrefab &&
                    treebotAiMaster.bodyPrefab.TryGetComponent(out CharacterBody treebotBody))
                {
                    _treebotAiSpawnCard.hullSize = treebotBody.hullClassification;
                }
                else
                {
                    Log.Error($"{_treebotAiMasterPrefab} is missing body prefab!");
                }

                args.ContentPack.spawnCards.Add(_treebotAiSpawnCard);
            });

            coroutine.Add(treebotMonsterMasterLoad);

            AsyncOperationHandle<GameObject> toolbotBodyLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Toolbot.ToolbotBody_prefab);
            coroutine.Add(toolbotBodyLoad);

            yield return coroutine;

            if (!toolbotBodyLoad.AssertLoaded() || !_treebotAiMasterPrefab)
                yield break;

            foreach (AkBank toolbotBankComponent in toolbotBodyLoad.Result.GetComponents<AkBank>())
            {
                AkBank treebotMasterBankComponent = _treebotAiMasterPrefab.AddComponent<AkBank>();
                treebotMasterBankComponent.data = toolbotBankComponent.data;
                treebotMasterBankComponent.triggerList = new List<int> { AkTriggerHandler.ON_ENABLE_TRIGGER_ID };
                treebotMasterBankComponent.unloadTriggerList = new List<int> { AkTriggerHandler.ON_DISABLE_TRIGGER_ID };

                Log.Debug($"Copied sound bank reference {toolbotBankComponent.data.ObjectReference.ObjectName}");
            }
        }

        [SystemInitializer]
        private static void Init()
        {
            Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Toolbot.RoboCratePod_prefab).OnSuccess(roboCratePod =>
            {
                _spawnPodPrefab = roboCratePod;
            });
        }

        private const float SpawnInterval = 30f;

        private Xoroshiro128Plus _rng;

        private readonly float[] _spawnTimers = new float[(int)QualityTier.Count];

        private void OnEnable()
        {
            _rng = new Xoroshiro128Plus(Run.instance.spawnRng.nextUlong);
        }

        private void FixedUpdate()
        {
            for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
            {
                ref float spawnTimer = ref _spawnTimers[(int)qualityTier];
                if (Body.master.IsDeployableSlotAvailable(TreebotBuddy.GetTreebotBuddyDeployableSlot(qualityTier)))
                {
                    spawnTimer -= Time.fixedDeltaTime;
                    if (spawnTimer <= 0f)
                    {
                        if (attemptSpawn(qualityTier))
                        {
                            spawnTimer = SpawnInterval;
                        }
                        else
                        {
                            spawnTimer = 1f; // 1s retry rather than spending the entire cooldown
                        }
                    }
                }
            }
        }

        private bool attemptSpawn(QualityTier spawnQualityTier)
        {
            DirectorPlacementRule placementRule = new DirectorPlacementRule
            {
                position = Body.footPosition,
                maxDistance = 30f,
                minDistance = 5f,
                placementMode = DirectorPlacementRule.PlacementMode.Approximate,
            };

            DirectorSpawnRequest spawnRequest = new DirectorSpawnRequest(_treebotAiSpawnCard, placementRule, _rng)
            {
                summonerBodyObject = gameObject,
                teamIndexOverride = Body.teamComponent.teamIndex,
                ignoreTeamMemberLimit = true,
                onSpawnedServer = onSpawnedServer,
            };

            void onSpawnedServer(SpawnCard.SpawnResult spawnResult)
            {
                if (!spawnResult.success ||
                    !spawnResult.spawnedInstance ||
                    !spawnResult.spawnedInstance.TryGetComponent(out CharacterMaster spawnedMaster))
                {
                    return;
                }

                spawnedMaster.inventory.GiveItemPermanent(ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(spawnQualityTier));

                DeployableSlot deployableSlot = TreebotBuddy.GetTreebotBuddyDeployableSlot(spawnQualityTier);
                if (deployableSlot != DeployableSlot.None)
                {
                    Deployable deployable = spawnedMaster.GetComponent<Deployable>();
                    Body.master.AddDeployable(deployable, deployableSlot);
                }

                CharacterBody spawnedBody = spawnedMaster.GetBody();
                if (spawnedBody)
                {
                    foreach (EntityStateMachine bodyStateMachine in spawnedBody.GetComponents<EntityStateMachine>())
                    {
                        bodyStateMachine.initialStateType = bodyStateMachine.mainStateType;
                    }

                    if (_spawnPodPrefab)
                    {
                        GameObject podObject = Instantiate(_spawnPodPrefab, spawnResult.position, spawnResult.rotation);
                        NetworkServer.Spawn(podObject);

                        VehicleSeat podSeat = podObject.GetComponent<VehicleSeat>();
                        podSeat.AssignPassenger(spawnedBody.gameObject);
                    }
                }
            }

            GameObject spawnedMaster = DirectorCore.instance.TrySpawnObject(spawnRequest);
            return spawnedMaster != null;
        }
    }
}
