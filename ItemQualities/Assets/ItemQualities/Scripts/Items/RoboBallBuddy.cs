using HG;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using MiscFixes.Modules;
using R2API;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Items;
using RoR2.Navigation;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    internal static class RoboBallBuddy
    {
        public readonly struct RoboBallBuddyPrefabInfo
        {
            public readonly GameObject MasterPrefab;
            public readonly CharacterSpawnCard SpawnCard;

            public RoboBallBuddyPrefabInfo(GameObject masterPrefab, CharacterSpawnCard spawnCard)
            {
                MasterPrefab = masterPrefab;
                SpawnCard = spawnCard;
            }
        }

        private static RoboBallBuddyPrefabInfo _qualityRoboBallRedInfo;
        public static ref readonly RoboBallBuddyPrefabInfo QualityRoboBallRed => ref _qualityRoboBallRedInfo;

        private static RoboBallBuddyPrefabInfo _qualityRoboBallGreenInfo;
        public static ref readonly RoboBallBuddyPrefabInfo QualityRoboBallGreen => ref _qualityRoboBallGreenInfo;

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            SkillFamily secondaryFamily = args.ContentPack.skillFamilies.Find("RoboBallBuddyQualitySecondaryFamily");
            if (!secondaryFamily)
            {
                Log.Error("Failed to find asset RoboBallBuddyQualitySecondaryFamily");
                args.ProgressReceiver.Report(1f);
                yield break;
            }

            PartitionedProgress<ReadableProgress<float>> progress = new PartitionedProgress<ReadableProgress<float>>(args.ProgressReceiver);

            ProgressPartition soundBankProgress = progress.AddPartition();
            ProgressPartition masterPrefabsProgress = progress.AddPartition();

            // 433AAE3B-3D70-49D4-9913-A0CE38969717: Boss_SolusWing
            AsyncOperationHandle<WwiseBankReference> solusWingBankLoad = Addressables.LoadAssetAsync<WwiseBankReference>(Wwise._433AAE3B_3D70_49D4_9913_A0CE38969717_asset);
            yield return solusWingBankLoad.AsProgressCoroutine(soundBankProgress);

            WwiseBankReference solusWingBankReference = null;
            if (solusWingBankLoad.AssertLoaded())
            {
                if (solusWingBankLoad.Result.ObjectName == "Boss_SolusWing")
                {
                    solusWingBankReference = solusWingBankLoad.Result;
                }
                else
                {
                    Log.Error($"Incorrect bank loaded, expected Boss_SolusWing, got {solusWingBankLoad.Result.ObjectName}");
                }
            }

            RoboBallBuddyPrefabInfo createQualityRoboBallBuddy(GameObject masterPrefab)
            {
                CharacterMaster master = masterPrefab.GetComponent<CharacterMaster>();

                // Skill Drivers
                {
                    AISkillDriver[] defaultSkillDrivers = masterPrefab.GetComponents<AISkillDriver>();

                    AISkillDriver fireGigaBeamAndFleeDriver = masterPrefab.AddComponent<AISkillDriver>();
                    {
                        fireGigaBeamAndFleeDriver.customName = "FireGigaBeamAndFlee";
                        fireGigaBeamAndFleeDriver.skillSlot = SkillSlot.Secondary;

                        // Selection Conditions
                        fireGigaBeamAndFleeDriver.requireSkillReady = true;
                        fireGigaBeamAndFleeDriver.minDistance = 0f;
                        fireGigaBeamAndFleeDriver.maxDistance = 40f;

                        // Behavior
                        fireGigaBeamAndFleeDriver.moveTargetType = AISkillDriver.TargetType.CurrentEnemy;
                        fireGigaBeamAndFleeDriver.activationRequiresAimTargetLoS = true;
                        fireGigaBeamAndFleeDriver.activationRequiresAimConfirmation = true;
                        fireGigaBeamAndFleeDriver.movementType = AISkillDriver.MovementType.FleeMoveTarget;
                        fireGigaBeamAndFleeDriver.aimType = AISkillDriver.AimType.AtMoveTarget;
                        fireGigaBeamAndFleeDriver.buttonPressType = AISkillDriver.ButtonPressType.Hold;

                        // Transition Behavior
                        fireGigaBeamAndFleeDriver.driverUpdateTimerOverride = 5f;
                        fireGigaBeamAndFleeDriver.noRepeat = true;
                    }

                    AISkillDriver fireGigaBeamAndStrafeDriver = masterPrefab.AddComponent<AISkillDriver>();
                    {
                        fireGigaBeamAndStrafeDriver.customName = "FireGigaBeamAndStrafe";
                        fireGigaBeamAndStrafeDriver.skillSlot = SkillSlot.Secondary;

                        // Selection Conditions
                        fireGigaBeamAndStrafeDriver.requireSkillReady = true;
                        fireGigaBeamAndStrafeDriver.minDistance = 40f;
                        fireGigaBeamAndStrafeDriver.maxDistance = 100f;

                        // Behavior
                        fireGigaBeamAndStrafeDriver.moveTargetType = AISkillDriver.TargetType.CurrentEnemy;
                        fireGigaBeamAndStrafeDriver.activationRequiresAimTargetLoS = true;
                        fireGigaBeamAndStrafeDriver.activationRequiresAimConfirmation = true;
                        fireGigaBeamAndStrafeDriver.movementType = AISkillDriver.MovementType.StrafeMovetarget;
                        fireGigaBeamAndStrafeDriver.aimType = AISkillDriver.AimType.AtMoveTarget;
                        fireGigaBeamAndStrafeDriver.buttonPressType = AISkillDriver.ButtonPressType.Hold;

                        // Transition Behavior
                        fireGigaBeamAndStrafeDriver.driverUpdateTimerOverride = 5f;
                        fireGigaBeamAndStrafeDriver.noRepeat = true;
                    }

                    // Move all default SkillDrivers to end
                    foreach (AISkillDriver defaultSkillDriver in defaultSkillDrivers)
                    {
                        AISkillDriver clonedSkillDriver = masterPrefab.CloneComponent(defaultSkillDriver);
                        GameObject.Destroy(defaultSkillDriver);

                        switch (clonedSkillDriver.customName)
                        {
                            case "ShootAndFlee":
                            case "StrafeAndShoot":
                                clonedSkillDriver.noRepeat = true;
                                break;
                        }
                    }
                }

                // Body
                {
                    GameObject bodyPrefab = master.bodyPrefab.InstantiateClone("Quality" + master.bodyPrefab.name);

                    GenericSkill secondarySkill = bodyPrefab.AddComponent<GenericSkill>();
                    secondarySkill.skillName = "QualityGigaBeam";
                    secondarySkill._skillFamily = secondaryFamily;

                    SkillLocator skillLocator = bodyPrefab.GetComponent<SkillLocator>();
                    skillLocator.secondary = secondarySkill;

                    args.ContentPack.bodyPrefabs.Add(bodyPrefab);
                    master.bodyPrefab = bodyPrefab;
                }

                if (solusWingBankReference)
                {
                    AkBank bank = master.bodyPrefab.AddComponent<AkBank>();
                    bank.data.ObjectReference = solusWingBankReference;
                }

                CharacterSpawnCard spawnCard = ScriptableObject.CreateInstance<CharacterSpawnCard>();
                spawnCard.name = "csc" + masterPrefab.name;
                spawnCard.prefab = masterPrefab;
                spawnCard.sendOverNetwork = true;
                spawnCard.hullSize = HullClassification.Human;
                spawnCard.nodeGraphType = MapNodeGroup.GraphType.Air;
                spawnCard.requiredFlags = NodeFlags.None;
                spawnCard.forbiddenFlags = NodeFlags.NoCharacterSpawn;

                args.ContentPack.masterPrefabs.Add(masterPrefab);
                args.ContentPack.spawnCards.Add(spawnCard);

                return new RoboBallBuddyPrefabInfo(masterPrefab, spawnCard);
            }

            ParallelProgressCoroutine loadMastersCoroutine = new ParallelProgressCoroutine(masterPrefabsProgress);

            // RoboBallRedBuddyMaster
            {
                AsyncOperationHandle<GameObject> masterLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_RoboBallBuddy.RoboBallRedBuddyMaster_prefab);
                masterLoad.OnSuccess(masterPrefab =>
                {
                    GameObject qualityMasterPrefab = masterPrefab.InstantiateClone("QualityRoboBallRedBuddyMaster");

                    _qualityRoboBallRedInfo = createQualityRoboBallBuddy(qualityMasterPrefab);
                });

                loadMastersCoroutine.Add(masterLoad);
            }

            // RoboBallGreenBuddyMaster
            {
                AsyncOperationHandle<GameObject> masterLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_RoboBallBuddy.RoboBallGreenBuddyMaster_prefab);
                masterLoad.OnSuccess(masterPrefab =>
                {
                    GameObject qualityMasterPrefab = masterPrefab.InstantiateClone("QualityRoboBallGreenBuddyMaster");

                    _qualityRoboBallGreenInfo = createQualityRoboBallBuddy(qualityMasterPrefab);
                });

                loadMastersCoroutine.Add(masterLoad);
            }

            yield return loadMastersCoroutine;
        }

        [SystemInitializer]
        private static void Init()
        {
            On.RoR2.Items.RoboBallBuddyBodyBehavior.CreateSpawners += RoboBallBuddyBodyBehavior_CreateSpawners;
            On.RoR2.Items.RoboBallBuddyBodyBehavior.DestroySpawners += RoboBallBuddyBodyBehavior_DestroySpawners;

            On.RoR2.Items.RoboBallBuddyBodyBehavior.OnMinionSpawnedServer += RoboBallBuddyBodyBehavior_OnMinionSpawnedServer;
        }

        private static void RoboBallBuddyBodyBehavior_CreateSpawners(On.RoR2.Items.RoboBallBuddyBodyBehavior.orig_CreateSpawners orig, RoboBallBuddyBodyBehavior self)
        {
            orig(self);

            if (self.TryGetComponent(out RoboBallBuddyQualityItemBehavior qualityBehavior))
            {
                qualityBehavior.UpdateBuddySpawners(self);
            }
        }

        private static void RoboBallBuddyBodyBehavior_DestroySpawners(On.RoR2.Items.RoboBallBuddyBodyBehavior.orig_DestroySpawners orig, RoboBallBuddyBodyBehavior self)
        {
            orig(self);

            if (self.TryGetComponent(out RoboBallBuddyQualityItemBehavior qualityBehavior))
            {
                qualityBehavior.UpdateBuddySpawners(self);
            }
        }

        private static void RoboBallBuddyBodyBehavior_OnMinionSpawnedServer(On.RoR2.Items.RoboBallBuddyBodyBehavior.orig_OnMinionSpawnedServer orig, RoboBallBuddyBodyBehavior self, SpawnCard.SpawnResult spawnResult)
        {
            orig(self, spawnResult);

            if (!spawnResult.success)
                return;

            if (!self.body && !self.body.inventory)
                return;

            QualityTier qualityTier = self.body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.RoboBallBuddy).HighestQuality;
            if (qualityTier == QualityTier.None)
                return;

            if (spawnResult.spawnedInstance &&
                spawnResult.spawnedInstance.TryGetComponent(out CharacterMaster master) &&
                master.inventory)
            {
                master.inventory.GiveItemPermanent(ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(qualityTier));
                master.inventory.GiveItemPermanent(ItemQualitiesContent.ItemQualityGroups.RoboBallBuddyItem.GetItemIndex(qualityTier));
            }
        }
    }

    public sealed class RoboBallBuddyQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.RoboBallBuddy;

        private struct RoboBallBuddyInfo
        {
            public readonly DeployableSlot DeployableSlot;
            public DeployableMinionSpawner Spawner;
            public SpawnCard DefaultSpawnCard;
            public QualityTier PrevQualityTier;

            public readonly ref readonly RoboBallBuddy.RoboBallBuddyPrefabInfo QualityPrefabInfo
            {
                get
                {
                    switch (DeployableSlot)
                    {
                        case DeployableSlot.RoboBallRedBuddy:
                            return ref RoboBallBuddy.QualityRoboBallRed;
                        case DeployableSlot.RoboBallGreenBuddy:
                            return ref RoboBallBuddy.QualityRoboBallGreen;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            public RoboBallBuddyInfo(DeployableSlot deployableSlot)
            {
                DeployableSlot = deployableSlot;
                Spawner = null;
                DefaultSpawnCard = null;
                PrevQualityTier = QualityTier.None;
            }
        }

        private RoboBallBuddyBodyBehavior _baseBodyBehavior;

        private RoboBallBuddyInfo _redBuddyInfo = new RoboBallBuddyInfo(DeployableSlot.RoboBallRedBuddy);
        private RoboBallBuddyInfo _greenBuddyInfo = new RoboBallBuddyInfo(DeployableSlot.RoboBallGreenBuddy);

        private void Start()
        {
            _baseBodyBehavior = GetComponent<RoboBallBuddyBodyBehavior>();

            UpdateBuddySpawners(_baseBodyBehavior);
        }

        private void OnDisable()
        {
            updateBuddiesItemCounts(ItemQualityCounts.zero);
        }

        public void UpdateBuddySpawners(RoboBallBuddyBodyBehavior baseBodyBehavior)
        {
            bool hasBaseSpawner = baseBodyBehavior;
            setBuddySpawner(ref _redBuddyInfo, hasBaseSpawner ? baseBodyBehavior.redBuddySpawner : null);
            setBuddySpawner(ref _greenBuddyInfo, hasBaseSpawner ? baseBodyBehavior.greenBuddySpawner : null);
        }

        private void setBuddySpawner(ref RoboBallBuddyInfo buddyInfo, DeployableMinionSpawner spawner)
        {
            if (ReferenceEquals(buddyInfo.Spawner, spawner))
            {
                return;
            }

            buddyInfo.Spawner = spawner;

            if (ReferenceEquals(buddyInfo.DefaultSpawnCard, null))
            {
                buddyInfo.DefaultSpawnCard = spawner?.spawnCard;
            }

            refreshBuddySpawnerSpawnCard(ref buddyInfo, Stacks.HighestQuality != QualityTier.None);
        }

        private void refreshBuddySpawnerSpawnCard(ref RoboBallBuddyInfo buddyInfo, bool useQualitySpawnCard)
        {
            if (buddyInfo.Spawner == null)
            {
                return;
            }

            SpawnCard desiredSpawnCard = useQualitySpawnCard ? buddyInfo.QualityPrefabInfo.SpawnCard : buddyInfo.DefaultSpawnCard;
            if (ReferenceEquals(buddyInfo.Spawner.spawnCard, desiredSpawnCard))
            {
                return;
            }

            buddyInfo.Spawner.spawnCard = desiredSpawnCard;

            // Spawn card changed, we need to respawn the buddy
            if (Body.master.deployablesList != null)
            {
                foreach (DeployableInfo deployableInfo in Body.master.deployablesList)
                {
                    if (deployableInfo.slot == buddyInfo.DeployableSlot &&
                        deployableInfo.deployable &&
                        deployableInfo.deployable.TryGetComponent(out CharacterMaster roboBallBuddyMaster))
                    {
                        roboBallBuddyMaster.TrueKill();
                    }
                }

                // Skip respawn wait
                buddyInfo.Spawner.respawnStopwatch = buddyInfo.Spawner.respawnInterval;
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            updateBuddiesItemCounts(Stacks);
        }

        private void updateBuddiesItemCounts(in ItemQualityCounts roboBallBuddyCounts)
        {
            updateBuddyItemCounts(ref _redBuddyInfo, roboBallBuddyCounts);
            updateBuddyItemCounts(ref _greenBuddyInfo, roboBallBuddyCounts);
        }

        private void updateBuddyItemCounts(ref RoboBallBuddyInfo buddyInfo, in ItemQualityCounts roboBallBuddyCounts)
        {
            using var _0 = ListPool<CharacterMaster>.RentCollection(out List<CharacterMaster> roboBallBuddyMasters);

            if (Body.master.deployablesList != null)
            {
                foreach (DeployableInfo deployableInfo in Body.master.deployablesList)
                {
                    if (deployableInfo.slot == buddyInfo.DeployableSlot &&
                        deployableInfo.deployable &&
                        deployableInfo.deployable.TryGetComponent(out CharacterMaster roboBallBuddyMaster))
                    {
                        roboBallBuddyMasters.Add(roboBallBuddyMaster);
                    }
                }
            }

            foreach (CharacterMaster roboBallBuddyMaster in roboBallBuddyMasters)
            {
                if (roboBallBuddyMaster.inventory)
                {
                    for (QualityTier itemQualityTier = 0; itemQualityTier < QualityTier.Count; itemQualityTier++)
                    {
                        ItemIndex itemIndex = ItemQualitiesContent.ItemQualityGroups.RoboBallBuddyItem.GetItemIndex(itemQualityTier);

                        int currentItemCount = roboBallBuddyMaster.inventory.GetItemCountPermanent(itemIndex);
                        int desiredItemCount = roboBallBuddyCounts[itemQualityTier];
                        int itemCountDiff = desiredItemCount - currentItemCount;
                        if (itemCountDiff != 0)
                        {
                            roboBallBuddyMaster.inventory.GiveItemPermanent(itemIndex, itemCountDiff);
                        }
                    }
                }
            }

            QualityTier qualityTier = roboBallBuddyCounts.HighestQuality;

            if (buddyInfo.PrevQualityTier != qualityTier)
            {
                ItemIndex fromItemIndex = ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(buddyInfo.PrevQualityTier);
                ItemIndex toItemIndex = ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(qualityTier);

                foreach (CharacterMaster roboBallBuddyMaster in roboBallBuddyMasters)
                {
                    if (roboBallBuddyMaster.inventory)
                    {
                        if (fromItemIndex != ItemIndex.None)
                        {
                            new Inventory.ItemTransformation
                            {
                                originalItemIndex = fromItemIndex,
                                newItemIndex = toItemIndex,
                                minToTransform = 1,
                                maxToTransform = 1,
                                allowWhenDisabled = true,
                                transformationType = ItemTransformationTypeIndex.None,
                            }.TryTransform(roboBallBuddyMaster.inventory, out _);
                        }
                        else
                        {
                            roboBallBuddyMaster.inventory.GiveItemPermanent(toItemIndex);
                        }
                    }
                }

                buddyInfo.PrevQualityTier = qualityTier;

                refreshBuddySpawnerSpawnCard(ref buddyInfo, qualityTier != QualityTier.None);
            }
        }
    }
}
