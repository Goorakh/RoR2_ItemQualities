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
            IL.RoR2.Inventory.UpdateEquipmentSetCount += Inventory_UpdateEquipmentSetCount;
        }

        private static void Inventory_UpdateEquipmentSetCount(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            /*  int num = this.CalculateEffectiveItemStacks(DLC3Content.Items.ExtraEquipment.itemIndex);
             *  IL_0000: ldarg.0
             *  IL_0001: ldsfld    class RoR2.ItemDef RoR2.DLC3Content/Items::ExtraEquipment
             *  IL_0006: callvirt  instance valuetype RoR2.ItemIndex RoR2.ItemDef::get_itemIndex()
             *  IL_000B: call      instance int32 RoR2.Inventory::CalculateEffectiveItemStacks(valuetype RoR2.ItemIndex)
             *  IL_0010: stloc.0
             */

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdarg(0),
                               x => x.MatchLdsfld(typeof(DLC3Content.Items), nameof(DLC3Content.Items.ExtraEquipment)),
                               x => x.MatchCallOrCallvirt<ItemDef>("get_" + nameof(ItemDef.itemIndex)),
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.CalculateEffectiveItemStacks))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<Inventory, int>>(getQualityEffectiveItemStacks);
            c.Emit(OpCodes.Add);

            static int getQualityEffectiveItemStacks(Inventory inventory)
            {
                return inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ExtraEquipment).TotalQualityCount;
            }
        }
    }

    internal sealed class ExtraEquipmentQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.ExtraEquipment;

        private static bool IsEquipmentDrone(CharacterMaster master)
        {
            return master && master.masterIndex.isValid && (master.masterIndex == MasterCatalog.FindMasterIndex("EquipmentDroneMaster") || master.masterIndex == MasterCatalog.FindMasterIndex("QualityEquipmentDroneMaster"));
        }

        public int targetBoostEquipmentRechargeCount
        {
            get
            {
                ref readonly var stacks = ref Stacks;
                return (stacks.UncommonCount * 1) + // 10%
                       (stacks.RareCount * 2) +     // 20%
                       (stacks.EpicCount * 3) +     // 30%
                       (stacks.LegendaryCount * 5); // 40%
            }
        }

        private QualityTier _lastEquipmentDroneQualityTier = QualityTier.None;
        private int _lastEquipmentDroneEquipmentRechargeCount = 0;
        private EquipmentDroneSlot[] _equipmentDrones = Array.Empty<EquipmentDroneSlot>();

        private int _equipmentDroneCount;
        private float _equipmentDroneRespawnTimer = 0f;

        private Xoroshiro128Plus _rng;

        private void OnEnable()
        {
            _rng = new Xoroshiro128Plus(Run.instance.seed ^ 2495764536);

            _lastEquipmentDroneQualityTier = QualityTier.None;

            _equipmentDroneCount = 0;
            _equipmentDroneRespawnTimer = 0f;

            Body.onInventoryChanged += onInventoryChanged;
            onInventoryChanged();

            MasterSummon.onServerMasterSummonGlobal += onServerMasterSummonGlobal;
        }

        private void OnDisable()
        {
            Body.onInventoryChanged -= onInventoryChanged;

            MasterSummon.onServerMasterSummonGlobal -= onServerMasterSummonGlobal;

            for (int i = 0; i < _equipmentDrones.Length; i++)
            {
                ref EquipmentDroneSlot droneSlot = ref _equipmentDrones[i];
                if (droneSlot != null)
                {
                    droneSlot.Clear();
                    droneSlot = null;
                }
            }

            MinionOwnership.MinionGroup minionGroup = Body.master ? MinionOwnership.MinionGroup.FindGroup(Body.master.netId) : null;
            if (minionGroup != null)
            {
                for (int i = 0; i < minionGroup.memberCount; i++)
                {
                    MinionOwnership minion = minionGroup.members[i];
                    if (minion &&
                        minion.TryGetComponent(out CharacterMaster minionMaster) &&
                        IsEquipmentDrone(minionMaster))
                    {
                        minionMaster.inventory.RemoveItemPermanent(RoR2Content.Items.BoostEquipmentRecharge, targetBoostEquipmentRechargeCount);
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            _equipmentDroneRespawnTimer -= Time.fixedDeltaTime;
            if (_equipmentDroneRespawnTimer <= 0f)
            {
                _equipmentDroneRespawnTimer = 1f;

                int respawnDroneIndex = -1;
                for (int i = 0; i < _equipmentDroneCount; i++)
                {
                    EquipmentDroneSlot droneSlot = _equipmentDrones[i];
                    if (droneSlot.IsEmpty && droneSlot.HeldEquipment != EquipmentIndex.None)
                    {
                        respawnDroneIndex = i;
                        break;
                    }
                }

                if (respawnDroneIndex != -1)
                {
                    _equipmentDrones[respawnDroneIndex].UpdateMaster();
                }
            }
        }

        private void onInventoryChanged()
        {
            var _ = ListPool<EquipmentIndex>.RentCollection(out List<EquipmentIndex> heldEquipments);

            if (!Body.inventory.GetEquipmentDisabled())
            {
                byte activeEquipmentSlot = Body.inventory.activeEquipmentSlot;
                byte activeEquipmentSet = ArrayUtils.GetSafe(Body.inventory.activeEquipmentSet, activeEquipmentSlot);

                int slotCount = Body.inventory.GetEquipmentSlotCount();

                ListUtils.EnsureCapacity(heldEquipments, slotCount * (Stacks.TotalCount + 1));

                for (uint slot = 0; slot < slotCount; slot++)
                {
                    for (uint set = 0, setCount = (uint)Body.inventory.GetEquipmentSetCount(slot); set < setCount; set++)
                    {
                        // Skip currently held equipment
                        if (slot == activeEquipmentSlot && set == activeEquipmentSet)
                        {
                            continue;
                        }

                        EquipmentState equipmentState = Body.inventory.GetEquipment(slot, set);
                        if (equipmentState.equipmentIndex != EquipmentIndex.None)
                        {
                            heldEquipments.Add(equipmentState.equipmentIndex);
                        }
                    }
                }
            }

            _equipmentDroneCount = heldEquipments.Count;
            ArrayUtils.EnsureCapacity(ref _equipmentDrones, _equipmentDroneCount);

            for (int i = 0; i < _equipmentDrones.Length; i++)
            {
                EquipmentDroneSlot droneSlot = _equipmentDrones[i] ??= new EquipmentDroneSlot(this);
                if (i < _equipmentDroneCount)
                {
                    droneSlot.SetHeldEquipment(heldEquipments[i]);
                }
                else
                {
                    droneSlot.Clear();
                }
            }
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();

            QualityTier equipmentDroneQualityTier = Stacks.HighestQuality;
            if (equipmentDroneQualityTier != _lastEquipmentDroneQualityTier)
            {
                ItemIndex previousQualityTierItemIndex = ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(_lastEquipmentDroneQualityTier);
                ItemIndex qualityTierItemIndex = ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(equipmentDroneQualityTier);

                foreach (EquipmentDroneSlot droneSlot in _equipmentDrones)
                {
                    if (droneSlot.Master && droneSlot.Master.inventory)
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
                            }.TryTransform(droneSlot.Master.inventory, out _);
                        }
                        else
                        {
                            droneSlot.Master.inventory.GiveItemPermanent(qualityTierItemIndex);
                        }
                    }
                }

                _lastEquipmentDroneQualityTier = equipmentDroneQualityTier;
            }

            int boostEquipmentRechargeCount = targetBoostEquipmentRechargeCount;
            if (boostEquipmentRechargeCount != _lastEquipmentDroneEquipmentRechargeCount)
            {
                MinionOwnership.MinionGroup minionGroup = Body.master ? MinionOwnership.MinionGroup.FindGroup(Body.master.netId) : null;
                if (minionGroup != null)
                {
                    for (int i = 0; i < minionGroup.memberCount; i++)
                    {
                        MinionOwnership minion = minionGroup.members[i];
                        if (minion &&
                            minion.TryGetComponent(out CharacterMaster minionMaster) &&
                            IsEquipmentDrone(minionMaster))
                        {
                            minionMaster.inventory.GiveItemPermanent(RoR2Content.Items.BoostEquipmentRecharge, boostEquipmentRechargeCount - _lastEquipmentDroneEquipmentRechargeCount);
                        }
                    }
                }

                _lastEquipmentDroneEquipmentRechargeCount = boostEquipmentRechargeCount;
            }
        }

        private void onServerMasterSummonGlobal(MasterSummon.MasterSummonReport summonReport)
        {
            if (ReferenceEquals(Body.master, null) || !ReferenceEquals(summonReport.leaderMasterInstance, Body.master))
            {
                return;
            }

            CharacterMaster summonedMaster = summonReport.summonMasterInstance;
            if (summonedMaster && IsEquipmentDrone(summonedMaster))
            {
                summonedMaster.inventory.GiveItemPermanent(RoR2Content.Items.BoostEquipmentRecharge, targetBoostEquipmentRechargeCount);
            }
        }

        private sealed class EquipmentDroneSlot
        {
            public readonly ExtraEquipmentQualityItemBehavior OwnerItemBehavior;

            public CharacterMaster Master { get; private set; }

            public EquipmentIndex HeldEquipment { get; private set; } = EquipmentIndex.None;

            public bool IsEmpty => !Master || Master.IsDeadAndOutOfLivesServer();

            public EquipmentDroneSlot(ExtraEquipmentQualityItemBehavior ownerItemBehavior)
            {
                OwnerItemBehavior = ownerItemBehavior;
            }

            public void Clear()
            {
                SetHeldEquipmentInternal(EquipmentIndex.None, true);
                UpdateMaster();
            }

            public void SetHeldEquipment(EquipmentIndex equipmentIndex)
            {
                SetHeldEquipmentInternal(equipmentIndex);
            }

            private void SetHeldEquipmentInternal(EquipmentIndex equipmentIndex, bool silent = false)
            {
                if (HeldEquipment != equipmentIndex)
                {
                    HeldEquipment = equipmentIndex;
                    UpdateMasterHeldEquipment(silent);
                }
            }

            private void UpdateMasterHeldEquipment(bool silent = false)
            {
                if (Master)
                {
                    Master.inventory.GiveItemPermanent(ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(OwnerItemBehavior.Stacks.HighestQuality));
                    Master.inventory.SetEquipmentIndex(HeldEquipment, HeldEquipment == EquipmentIndex.None);

                    CharacterBody droneBody = Master.GetBody();
                    if (HeldEquipment != EquipmentIndex.None)
                    {
                        if (!silent && ItemQualitiesContent.Prefabs.PickupTransferOrbEffect && droneBody)
                        {
                            const float TransferOrbEffectDuration = 1f;

                            EffectData effectData = new EffectData
                            {
                                origin = OwnerItemBehavior.Body.corePosition,
                                genericUInt = Util.IntToUintPlusOne(PickupCatalog.FindPickupIndex(HeldEquipment).value),
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

            public void UpdateMaster()
            {
                bool hasDrone = !IsEmpty;
                bool shouldHaveDrone = HeldEquipment != EquipmentIndex.None;

                if (shouldHaveDrone != hasDrone)
                {
                    if (shouldHaveDrone)
                    {
                        DirectorPlacementRule placementRule = new DirectorPlacementRule
                        {
                            position = OwnerItemBehavior.Body.corePosition,
                            placementMode = DirectorPlacementRule.PlacementMode.Approximate,
                            minDistance = 5f,
                            maxDistance = 20f,
                        };

                        DirectorSpawnRequest spawnRequest = new DirectorSpawnRequest(ExtraEquipment.QualityEquipmentDroneSpawnCard, placementRule, OwnerItemBehavior._rng)
                        {
                            summonerBodyObject = OwnerItemBehavior.gameObject,
                            teamIndexOverride = OwnerItemBehavior.Body.teamComponent.teamIndex,
                        };

                        spawnRequest.onSpawnedServer += OnDroneSpawnedServer;

                        DirectorCore.instance.TrySpawnObject(spawnRequest);
                    }
                    else
                    {
                        Master.TrueKill();
                        Master = null;
                    }

                    hasDrone = shouldHaveDrone;
                }
            }

            private void OnDroneSpawnedServer(SpawnCard.SpawnResult result)
            {
                if (result.success && result.spawnedInstance.TryGetComponent(out CharacterMaster master))
                {
                    Master = master;
                    UpdateMasterHeldEquipment(true);
                }
            }
        }
    }
}
