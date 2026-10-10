using HG;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Navigation;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    internal static class ExtraEquipment
    {
        // Hook Inventory.UpdateEquipmentSetCount

        public static GameObject QualityEquipmentDroneMasterPrefab { get; private set; }

        public static CharacterSpawnCard QualityEquipmentDroneSpawnCard { get; private set; }

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            ParallelProgressCoroutine coroutine = new ParallelProgressCoroutine(args.ProgressReceiver);

            AsyncOperationHandle<GameObject> equipmentDroneMasterHandle = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Drones.EquipmentDroneMaster_prefab);
            AsyncOperationHandle<DroneDef> equipmentDroneHandle = AddressableUtil.LoadTempAssetAsync<DroneDef>(RoR2_Base_Drones.EquipmentDrone_asset);

            coroutine.Add(equipmentDroneMasterHandle);
            coroutine.Add(equipmentDroneHandle);
            yield return coroutine;

            if (!equipmentDroneMasterHandle.AssertLoaded() || !equipmentDroneHandle.AssertLoaded())
                yield break;

            GameObject qualityEquipmentDroneMasterPrefab = equipmentDroneMasterHandle.Result.InstantiateClone("QualityEquipmentDroneMaster");
            CharacterMaster qualityEquipmentDroneMaster = qualityEquipmentDroneMasterPrefab.GetComponent<CharacterMaster>();

            // Master
            {
                GameObject.Destroy(qualityEquipmentDroneMasterPrefab.GetComponent<SetDontDestroyOnLoad>());

                QualityEquipmentDroneMasterPrefab = qualityEquipmentDroneMasterPrefab;
                args.ContentPack.masterPrefabs.Add(qualityEquipmentDroneMasterPrefab);
            }

            GameObject qualityEquipmentDroneBodyPrefab = qualityEquipmentDroneMaster.bodyPrefab.InstantiateClone("QualityEquipmentDroneBody");
            qualityEquipmentDroneMaster.bodyPrefab = qualityEquipmentDroneBodyPrefab;
            CharacterBody qualityEquipmentDroneBody = qualityEquipmentDroneBodyPrefab.GetComponent<CharacterBody>();

            // Body
            {
                args.ContentPack.bodyPrefabs.Add(qualityEquipmentDroneBodyPrefab);

                // Skin
                {
                    if (qualityEquipmentDroneBodyPrefab.TryGetComponent(out ModelLocator modelLocator) &&
                        modelLocator.modelTransform &&
                        modelLocator.modelTransform.TryGetComponent(out ModelSkinController modelSkinController) &&
                        modelSkinController.skins != null &&
                        modelSkinController.skins.Length > 0)
                    {

                        SkinnedMeshRenderer equipmentDroneMainBodyRenderer = modelLocator.modelTransform.GetComponentInChildren<SkinnedMeshRenderer>();
                        if (equipmentDroneMainBodyRenderer)
                        {
                            SkinDefParams qualitySkinParams = ScriptableObject.CreateInstance<SkinDefParams>();
                            qualitySkinParams.name = "skinEquipmentDroneQuality_params";
                            qualitySkinParams.rendererInfos = new CharacterModel.RendererInfo[]
                            {
                                new CharacterModel.RendererInfo
                                {
                                    renderer = equipmentDroneMainBodyRenderer,
                                    defaultMaterial = args.ContentPack.materials.Find("mat" + nameof(ItemQualitiesContent.Materials.TrimSheetQualityEquipmentDrone)),
                                    defaultShadowCastingMode = equipmentDroneMainBodyRenderer.shadowCastingMode,
                                    hideOnDeath = false,
                                    ignoreOverlays = false,
                                    ignoresMaterialOverrides = false,
                                }
                            };

                            SkinDef qualitySkin = ScriptableObject.CreateInstance<SkinDef>();
                            qualitySkin.name = "skinEquipmentDroneQuality";
                            qualitySkin.baseSkins = new SkinDef[] { modelSkinController.skins[0] };
                            qualitySkin.rootObject = modelLocator.modelTransform.gameObject;
                            qualitySkin.skinDefParams = qualitySkinParams;

                            modelSkinController.skins = new SkinDef[] { qualitySkin };
                        }
                    }
                }
            }

            // DroneDef
            {
                DroneDef qualityEquipmentDrone = DroneDef.Instantiate(equipmentDroneHandle.Result);
                qualityEquipmentDrone.name = "QualityEquipmentDrone";
                qualityEquipmentDrone.remoteOpBody = null;
                qualityEquipmentDrone.droneBrokenSpawnCard = null;
                qualityEquipmentDrone.canDrop = false;
                qualityEquipmentDrone.canCombine = false;
                qualityEquipmentDrone.canScrap = false;
                qualityEquipmentDrone._masterPrefab = null;
                qualityEquipmentDrone.bodyPrefab = qualityEquipmentDroneBodyPrefab;

                args.ContentPack.droneDefs.Add(qualityEquipmentDrone);
            }

            // SpawnCard
            {
                QualityEquipmentDroneSpawnCard = ScriptableObject.CreateInstance<CharacterSpawnCard>();
                QualityEquipmentDroneSpawnCard.name = "cscQualityEquipmentDrone";
                QualityEquipmentDroneSpawnCard.prefab = qualityEquipmentDroneMasterPrefab;
                QualityEquipmentDroneSpawnCard.sendOverNetwork = true;
                QualityEquipmentDroneSpawnCard.hullSize = qualityEquipmentDroneBody.hullClassification;
                QualityEquipmentDroneSpawnCard.nodeGraphType = MapNodeGroup.GraphType.Air;
                QualityEquipmentDroneSpawnCard.requiredFlags = NodeFlags.None;
                QualityEquipmentDroneSpawnCard.forbiddenFlags = NodeFlags.NoCharacterSpawn;

                args.ContentPack.spawnCards.Add(QualityEquipmentDroneSpawnCard);
            }
        }

        [InitDuringStartupPhase(GameInitPhase.PreSplash)]
        private static void Init()
        {
            IL.RoR2.Inventory.RecalculateEquipmentSetsServer += Inventory_RecalculateEquipmentSetsServer;
        }

        private static void Inventory_RecalculateEquipmentSetsServer(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            /*  b = HGMath.ByteSafeAddUInt(b, (uint)this.GetItemCountTotal(DLC3Content.Items.ExtraEquipment));
             *  IL_0017: ldloc.0
             *  IL_0018: ldarg.0
             *  IL_0019: ldsfld    class RoR2.ItemDef RoR2.DLC3Content/Items::ExtraEquipment
             *  IL_001E: call      instance int32 RoR2.Inventory::GetItemCountTotal(class RoR2.ItemDef)
             *  IL_0023: call      uint8 HGMath::ByteSafeAddUInt(uint8, uint32)
             *  IL_0028: stloc.0
             */

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdarg(0),
                               x => x.MatchLdsfld(typeof(DLC3Content.Items), nameof(DLC3Content.Items.ExtraEquipment)),
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCountTotal))))
            {
                Log.PatchError(il, "Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<Inventory, int>>(getQualityEffectiveItemStacks);
            c.Emit(OpCodes.Add);

            static int getQualityEffectiveItemStacks(Inventory inventory)
            {
                return inventory.GetItemCountsTotal(ItemQualitiesContent.ItemQualityGroups.ExtraEquipment).TotalQualityCount;
            }
        }
    }

    internal sealed class ExtraEquipmentQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.ExtraEquipment;

        private static bool IsEquipmentDrone(MasterCatalog.MasterIndex masterIndex)
        {
            return masterIndex == MasterCatalog.FindMasterIndex("EquipmentDroneMaster") ||
                   masterIndex == MasterCatalog.FindMasterIndex("QualityEquipmentDroneMaster");
        }

        public int droneBoostEquipmentRechargeCount
        {
            get
            {
                ref readonly ItemQualityCounts stacks = ref Stacks;
                return (stacks.UncommonCount * 1) + // 10%
                       (stacks.RareCount * 2) +     // 20%
                       (stacks.EpicCount * 3) +     // 30%
                       (stacks.LegendaryCount * 4); // 40%
            }
        }

        private readonly EquipmentMap<EquipmentDroneSpawner> equipmentDroneSpawners = new EquipmentMap<EquipmentDroneSpawner>(1, 2);

        private Xoroshiro128Plus _rng;

        private int previousDroneBoostEquipmentRechargeCount = 0;
        private QualityTier previousQualityTier = QualityTier.None;
        private EquipmentLocation previousEquipmentLocation = EquipmentLocation.invalid;

        private void OnEnable()
        {
            _rng = new Xoroshiro128Plus(Run.instance.seed ^ 2495764536);

            equipmentDroneSpawners.onEquipmentLost += onEquipmentDroneSpawnerLost;

            onInventoryChanged();

            if (Body && Body.inventory)
            {
                Body.inventory.onInventoryChanged += onInventoryChanged;
                Body.inventory.onActiveEquipmentLocationChanged += onActiveEquipmentLocationChanged;
            }

            MasterSummon.onServerMasterSummonGlobal += onServerMasterSummonGlobal;
        }

        private void OnDisable()
        {
            MasterSummon.onServerMasterSummonGlobal -= onServerMasterSummonGlobal;

            if (!ReferenceEquals(Body?.inventory, null))
            {
                Body.inventory.onInventoryChanged -= onInventoryChanged;
                Body.inventory.onActiveEquipmentLocationChanged -= onActiveEquipmentLocationChanged;
            }

            // Will invoke onEquipmentLost event for all spawners
            equipmentDroneSpawners.Resize(0, 0);
        }

        private void onInventoryChanged()
        {
            ref readonly ItemQualityCounts stacks = ref Stacks;

            uint slotCount = Body.inventory.equipmentSlotCount;
            uint setCount = Body.inventory.accessibleEquipmentSetCount;

            equipmentDroneSpawners.Resize(slotCount, setCount);

            EquipmentLocation activeEquipmentLocation = Body.inventory.activeEquipmentLocation;

            QualityTier qualityTier = stacks.HighestQuality;

            ItemIndex previousQualityTierItemIndex = ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(previousQualityTier);
            ItemIndex qualityTierItemIndex = ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(qualityTier);

            for (uint slot = 0; slot < slotCount; slot++)
            {
                for (uint set = 0; set < setCount; set++)
                {
                    EquipmentLocation location = new EquipmentLocation { slot = slot, set = set };
                    EquipmentState equipmentState = Body.inventory.GetEquipment(location);

                    ref EquipmentDroneSpawner droneSpawner = ref equipmentDroneSpawners[location];

                    bool hasSpawner = droneSpawner != null;
                    bool shouldHaveSpawner = !equipmentState.isDisabled && equipmentState.equipmentIndex != EquipmentIndex.None && location != activeEquipmentLocation;

                    if (hasSpawner != shouldHaveSpawner)
                    {
                        if (shouldHaveSpawner)
                        {
                            droneSpawner = new EquipmentDroneSpawner(Body.master, ExtraEquipment.QualityEquipmentDroneSpawnCard, 1f, _rng, 1);
                            droneSpawner.SetHeldEquipmentServer(getHeldEquipmentIndex(equipmentState.equipmentIndex));
                            droneSpawner.onDroneSpawnedServer += onDroneSpawnedServer;
                        }
                        else
                        {
                            onEquipmentDroneSpawnerLost(droneSpawner, location);
                            droneSpawner = null;
                        }

                        hasSpawner = shouldHaveSpawner;
                    }

                    if (hasSpawner)
                    {
                        if (qualityTier != previousQualityTier)
                        {
                            foreach (CharacterMaster droneMaster in droneSpawner.droneMasters)
                            {
                                if (previousQualityTierItemIndex != ItemIndex.None)
                                {
                                    new Inventory.ItemTransformation
                                    {
                                        originalItemIndex = previousQualityTierItemIndex,
                                        newItemIndex = qualityTierItemIndex,
                                        minToTransform = 1,
                                        maxToTransform = 1,
                                        allowWhenDisabled = true,
                                        transformationType = ItemTransformationTypeIndex.None,
                                    }.TryTransform(droneMaster.inventory, out _);
                                }
                                else
                                {
                                    droneMaster.inventory.GiveItemPermanent(qualityTierItemIndex);
                                }
                            }
                        }
                    }
                }
            }

            int newDroneBoostEquipmentRechargeCount = droneBoostEquipmentRechargeCount;
            if (previousDroneBoostEquipmentRechargeCount != newDroneBoostEquipmentRechargeCount)
            {
                int diff = newDroneBoostEquipmentRechargeCount - previousDroneBoostEquipmentRechargeCount;

                MinionOwnership.MinionGroup minionGroup = Body.master ? MinionOwnership.MinionGroup.FindGroup(Body.master.netId) : null;
                if (minionGroup != null)
                {
                    int minionCount = minionGroup.memberCount;
                    for (int i = 0; i < minionCount; i++)
                    {
                        MinionOwnership minion = minionGroup.members[i];
                        if (minion &&
                            minion.TryGetComponent(out CharacterMaster minionMaster) &&
                            minionMaster.originalBodyPrefab &&
                            minionMaster.originalBodyPrefab.TryGetComponent(out CharacterBody minionBodyPrefab) &&
                            (minionBodyPrefab.bodyFlags & CharacterBody.BodyFlags.Mechanical) != 0)
                        {
                            minionMaster.inventory.GiveItemPermanent(RoR2Content.Items.BoostEquipmentRecharge, diff);
                        }
                    }
                }

                previousDroneBoostEquipmentRechargeCount = newDroneBoostEquipmentRechargeCount;
            }

            previousQualityTier = qualityTier;
        }

        private void onDroneSpawnedServer(CharacterMaster droneMaster)
        {
            ItemIndex qualityTierItemIndex = ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(previousQualityTier);
            if (qualityTierItemIndex != ItemIndex.None)
            {
                droneMaster.inventory.GiveItemPermanent(qualityTierItemIndex);
            }
        }

        private void onServerMasterSummonGlobal(MasterSummon.MasterSummonReport summonReport)
        {
            if (summonReport.summonMasterInstance && summonReport.summonBodyInstance && (summonReport.summonBodyInstance.bodyFlags & CharacterBody.BodyFlags.Mechanical) != 0)
            {
                summonReport.summonMasterInstance.inventory.GiveItemPermanent(RoR2Content.Items.BoostEquipmentRecharge, droneBoostEquipmentRechargeCount);
            }
        }

        private void onEquipmentDroneSpawnerLost(in EquipmentDroneSpawner droneSpawner, in EquipmentLocation location)
        {
            if (droneSpawner != null)
            {
                droneSpawner.onDroneSpawnedServer -= onDroneSpawnedServer;

                foreach (CharacterMaster master in droneSpawner.droneMasters)
                {
                    master.TrueKill();
                }

                droneSpawner.Dispose();
            }
        }

        private EquipmentIndex getHeldEquipmentIndex(EquipmentIndex equipmentIndex)
        {
            QualityTier equipmentQualityTier = QualityCatalog.GetQualityTier(equipmentIndex);
            EquipmentQualityGroupIndex equipmentGroupIndex = QualityCatalog.FindEquipmentQualityGroupIndex(equipmentIndex);
            if (equipmentGroupIndex != EquipmentQualityGroupIndex.Invalid)
            {
                EquipmentQualityGroupIndex convertToEquipmentGroupIndex = EquipmentQualityGroupIndex.Invalid;
                if (equipmentGroupIndex == ItemQualitiesContent.EquipmentQualityGroups.BossHunter.GroupIndex)
                {
                    convertToEquipmentGroupIndex = ItemQualitiesContent.EquipmentQualityGroups.BossHunterConsumed.GroupIndex;
                }
                else if (equipmentGroupIndex == ItemQualitiesContent.EquipmentQualityGroups.HealAndRevive.GroupIndex)
                {
                    convertToEquipmentGroupIndex = ItemQualitiesContent.EquipmentQualityGroups.HealAndReviveConsumed.GroupIndex;
                }

                if (convertToEquipmentGroupIndex != EquipmentQualityGroupIndex.Invalid)
                {
                    EquipmentQualityGroup convertToEquipmentGroup = QualityCatalog.GetEquipmentQualityGroup(convertToEquipmentGroupIndex);

                    equipmentIndex = convertToEquipmentGroup.GetEquipmentIndex(equipmentQualityTier);
                    equipmentGroupIndex = convertToEquipmentGroup.GroupIndex;
                }
            }
            else
            {
                if (equipmentIndex == DLC4Content.Equipment.SpawnAltar.equipmentIndex)
                {
                    // shrug
                    equipmentIndex = EquipmentIndex.None;
                }
            }

            return equipmentIndex;
        }

        private void onActiveEquipmentLocationChanged()
        {
            EquipmentLocation activeEquipmentLocation = Body.inventory.activeEquipmentLocation;
            if (activeEquipmentLocation == previousEquipmentLocation)
            {
                return;
            }

            if (previousEquipmentLocation.isValid)
            {
                ref EquipmentDroneSpawner prevDroneSpawner = ref equipmentDroneSpawners[previousEquipmentLocation];
                ref EquipmentDroneSpawner activeDroneSpawner = ref equipmentDroneSpawners[activeEquipmentLocation];

                (prevDroneSpawner, activeDroneSpawner) = (activeDroneSpawner, prevDroneSpawner);

                // Update held equipments after swap
                if (prevDroneSpawner != null)
                {
                    prevDroneSpawner.SetHeldEquipmentServer(getHeldEquipmentIndex(Body.inventory.GetEquipment(previousEquipmentLocation).equipmentIndex));
                }

                if (activeDroneSpawner != null)
                {
                    activeDroneSpawner.SetHeldEquipmentServer(getHeldEquipmentIndex(Body.inventory.GetEquipment(activeEquipmentLocation).equipmentIndex));
                }
            }

            previousEquipmentLocation = activeEquipmentLocation;
        }

        internal sealed class EquipmentDroneSpawner : IDisposable
        {
            public readonly SpawnCard spawnCard;
            public readonly CharacterMaster ownerMaster;
            public readonly int maxDrones;

            public float spawnInterval;

            private readonly Xoroshiro128Plus rng;

            private float spawnStopwatch;

            private List<CharacterMaster> spawnedDroneMasters;
            public ReadOnlyList<CharacterMaster> droneMasters => spawnedDroneMasters;

            private EquipmentIndex currentEquipmentIndex = EquipmentIndex.None;

            public event Action<CharacterMaster> onDroneSpawnedServer;

            public EquipmentDroneSpawner(CharacterMaster ownerMaster, SpawnCard spawnCard, float spawnInterval, Xoroshiro128Plus rng, int maxDrones)
            {
                this.spawnCard = spawnCard;
                this.ownerMaster = ownerMaster;
                this.spawnInterval = spawnInterval;
                this.maxDrones = maxDrones;
                this.rng = rng;

                spawnStopwatch = rng.RangeFloat(0.3f, 1f) * spawnInterval;

                spawnedDroneMasters = ListPool<CharacterMaster>.RentCollection();

                RoR2Application.onFixedUpdate += onFixedUpdate;
            }

            public void Dispose()
            {
                RoR2Application.onFixedUpdate -= onFixedUpdate;

                spawnedDroneMasters = ListPool<CharacterMaster>.ReturnCollection(spawnedDroneMasters);
            }

            private void onFixedUpdate()
            {
                spawnStopwatch -= Time.fixedDeltaTime;
                if (spawnStopwatch <= 0f)
                {
                    spawnStopwatch = spawnInterval;

                    int droneCount = 0;
                    for (int i = spawnedDroneMasters.Count - 1; i >= 0; i--)
                    {
                        CharacterMaster master = spawnedDroneMasters[i];
                        if (master && !master.IsDeadAndOutOfLivesServer())
                        {
                            droneCount++;
                        }
                        else
                        {
                            spawnedDroneMasters.RemoveAt(i);
                        }
                    }

                    if (droneCount < maxDrones && ownerMaster && ownerMaster.TryGetBody(out CharacterBody ownerBody))
                    {
                        DirectorPlacementRule placementRule = new DirectorPlacementRule
                        {
                            position = ownerBody.corePosition,
                            placementMode = DirectorPlacementRule.PlacementMode.Approximate,
                            minDistance = 5f,
                            maxDistance = 35f,
                        };

                        DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(spawnCard, placementRule, rng)
                        {
                            ignoreTeamMemberLimit = true,
                            summonerBodyObject = ownerBody.gameObject,
                            teamIndexOverride = ownerBody.teamComponent.teamIndex,
                            onSpawnedServer = onDroneSpawnedServerInternal,
                        });
                    }
                }
            }

            private void onDroneSpawnedServerInternal(SpawnCard.SpawnResult spawnResult)
            {
                if (spawnResult.success && spawnResult.spawnedInstance && spawnResult.spawnedInstance.TryGetComponent(out CharacterMaster spawnedDroneMaster))
                {
                    if (currentEquipmentIndex != EquipmentIndex.None)
                    {
                        spawnedDroneMaster.inventory.SetEquipmentIndex(currentEquipmentIndex, spawnedDroneMaster.inventory.activeEquipmentLocation);
                    }

                    spawnedDroneMasters.Add(spawnedDroneMaster);

                    onDroneSpawnedServer?.Invoke(spawnedDroneMaster);
                }
            }

            public void SetHeldEquipmentServer(EquipmentIndex newEquipmentIndex)
            {
                if (currentEquipmentIndex == newEquipmentIndex)
                {
                    return;
                }

                currentEquipmentIndex = newEquipmentIndex;

                CharacterBody ownerBody = ownerMaster ? ownerMaster.GetBody() : null;

                foreach (CharacterMaster droneMaster in spawnedDroneMasters)
                {
                    if (!droneMaster || !droneMaster.inventory)
                    {
                        continue;
                    }

                    droneMaster.inventory.SetEquipmentIndex(newEquipmentIndex, droneMaster.inventory.activeEquipmentLocation);

                    if (newEquipmentIndex != EquipmentIndex.None)
                    {
                        if (droneMaster.TryGetBody(out CharacterBody droneBody) && ItemQualitiesContent.Prefabs.PickupTransferOrbEffect && ownerBody)
                        {
                            const float TransferOrbEffectDuration = 1f;

                            EffectData effectData = new EffectData
                            {
                                origin = ownerBody.corePosition,
                                genericUInt = Util.IntToUintPlusOne(PickupCatalog.FindPickupIndex(newEquipmentIndex).value),
                                genericFloat = TransferOrbEffectDuration,
                            };

                            if (droneBody.mainHurtBox)
                            {
                                effectData.SetHurtBoxReference(droneBody.mainHurtBox);
                            }
                            else
                            {
                                effectData.SetNetworkedObjectReference(droneBody.gameObject);
                            }

                            EffectManager.SpawnEffect(ItemQualitiesContent.Prefabs.PickupTransferOrbEffect, effectData, true);
                        }
                    }
                }
            }
        }
    }
}
