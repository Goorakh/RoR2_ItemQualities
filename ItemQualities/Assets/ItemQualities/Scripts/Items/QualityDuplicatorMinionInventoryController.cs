using HG;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    [RequireComponent(typeof(GenericOwnership))]
    [RequireComponent(typeof(Inventory))]
    public sealed class QualityDuplicatorMinionInventoryController : NetworkBehaviour
    {
        private GenericOwnership _ownership;
        private Inventory _minionMirrorInventory;

        private GameObject _ownerMasterObject;
        private CharacterMaster _ownerMaster;
        private NetworkInstanceId _ownerMasterNetworkId = NetworkInstanceId.Invalid;

        private int[] _previousItemStacks = Array.Empty<int>();

        private readonly List<ItemGrantEffectStep> _itemGrantEffectsQueue = new List<ItemGrantEffectStep>();
        private readonly struct ItemGrantEffectStep
        {
            public readonly CharacterMaster TargetMaster;
            public readonly ItemIndex ItemIndex;
            public readonly float Duration;
            public readonly Vector3 Origin;
            public readonly Run.FixedTimeStamp StartTime;

            public ItemGrantEffectStep(CharacterMaster targetMaster,
                                       ItemIndex itemIndex,
                                       float duration,
                                       Vector3 origin,
                                       Run.FixedTimeStamp startTime)
            {
                TargetMaster = targetMaster;
                Duration = duration;
                ItemIndex = itemIndex;
                StartTime = startTime;
                Origin = origin;
            }

            public readonly struct StartTimeComparer : IComparer<ItemGrantEffectStep>
            {
                public readonly int Compare(ItemGrantEffectStep x, ItemGrantEffectStep y)
                {
                    return x.StartTime.CompareTo(y.StartTime);
                }
            }
        }

        public Inventory MinionMirrorInventory => _minionMirrorInventory;

        public CharacterMaster OwnerMaster => _ownerMaster;

        public static event Action<QualityDuplicatorMinionInventoryController> OnOwnerLostGlobal;
        public static event Action<QualityDuplicatorMinionInventoryController> OnOwnerDiscoveredGlobal;

        private static EffectIndex _itemTransferOrbEffectIndex = EffectIndex.Invalid;

        [SystemInitializer]
        private static void Init()
        {
            _itemTransferOrbEffectIndex = EffectCatalogUtils.FindEffectIndex("ItemTransferOrbEffect");
            if (_itemTransferOrbEffectIndex == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find ItemTransferOrbEffect effect index");
            }
        }

        private void Awake()
        {
            _ownership = GetComponent<GenericOwnership>();
            _minionMirrorInventory = GetComponent<Inventory>();

            _previousItemStacks = ItemCatalog.RequestItemStackArray();
        }

        private void OnDestroy()
        {
            ItemCatalog.ReturnItemStackArray(_previousItemStacks);
            _previousItemStacks = Array.Empty<int>();
        }

        private void OnEnable()
        {
            InstanceTracker.Add(this);

            _minionMirrorInventory.onInventoryChanged += onMinionMirrorInventoryChanged;

            setOwnerMasterObject(_ownership.ownerObject);
            _ownership.onOwnerChanged += setOwnerMasterObject;
        }

        private void OnDisable()
        {
            _minionMirrorInventory.onInventoryChanged -= onMinionMirrorInventoryChanged;

            _ownership.onOwnerChanged -= setOwnerMasterObject;
            setOwnerMasterObject(null);

            Array.Fill(_previousItemStacks, 0);

            InstanceTracker.Remove(this);
        }

        private void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                while (_itemGrantEffectsQueue.Count > 0 && _itemGrantEffectsQueue[0].StartTime.hasPassed)
                {
                    ItemGrantEffectStep itemGrantEffect = ListUtils.TakeFirst(_itemGrantEffectsQueue);

                    if (itemGrantEffect.TargetMaster)
                    {
                        CharacterBody minionBody = itemGrantEffect.TargetMaster.GetBody();
                        if (minionBody)
                        {
                            const float ItemTransferOrbDuration = 1f;

                            EffectData effectData = new EffectData
                            {
                                origin = itemGrantEffect.Origin,
                                genericFloat = ItemTransferOrbDuration,
                                genericUInt = Util.IntToUintPlusOne((int)itemGrantEffect.ItemIndex)
                            };

                            if (minionBody.mainHurtBox)
                            {
                                effectData.SetHurtBoxReference(minionBody.mainHurtBox);
                            }
                            else
                            {
                                effectData.SetNetworkedObjectReference(minionBody.gameObject);
                            }

                            EffectManager.SpawnEffect(_itemTransferOrbEffectIndex, effectData, true);
                        }
                    }
                }
            }
        }

        private void setOwnerMasterObject(GameObject newOwnerMasterObject)
        {
            if (ReferenceEquals(newOwnerMasterObject, _ownerMasterObject))
            {
                return;
            }

            if (!ReferenceEquals(_ownerMaster, null))
            {
                OnOwnerLostGlobal?.Invoke(this);

                MinionHooks.OnMinionGroupMemberDiscoveredGlobal -= onMinionGroupMemberDiscoveredGlobal;
                MinionHooks.OnMinionGroupMemberLostGlobal -= onMinionGroupMemberLostGlobal;

                MinionOwnership.MinionGroup ownerMinionGroup = MinionOwnership.MinionGroup.FindGroup(_ownerMasterNetworkId);
                if (ownerMinionGroup != null)
                {
                    for (int i = ownerMinionGroup.memberCount - 1; i >= 0; i--)
                    {
                        MinionOwnership member = ownerMinionGroup.members[i];
                        if (member && member.TryGetComponent(out CharacterMaster minionMaster))
                        {
                            onMinionMasterLost(minionMaster);
                        }
                    }
                }
            }

            _ownerMasterObject = newOwnerMasterObject;
            _ownerMaster = _ownerMasterObject ? _ownerMasterObject.GetComponent<CharacterMaster>() : null;
            NetworkIdentity ownerMasterNetworkIdentity = _ownerMasterObject ? _ownerMasterObject.GetComponent<NetworkIdentity>() : null;
            _ownerMasterNetworkId = ownerMasterNetworkIdentity ? ownerMasterNetworkIdentity.netId : NetworkInstanceId.Invalid;

            if (!ReferenceEquals(_ownerMaster, null))
            {
                MinionOwnership.MinionGroup ownerMinionGroup = MinionOwnership.MinionGroup.FindGroup(_ownerMasterNetworkId);
                if (ownerMinionGroup != null)
                {
                    for (int i = 0; i < ownerMinionGroup.memberCount; i++)
                    {
                        MinionOwnership member = ownerMinionGroup.members[i];
                        if (member && member.TryGetComponent(out CharacterMaster minionMaster))
                        {
                            onMinionMasterDiscovered(minionMaster);
                        }
                    }
                }

                MinionHooks.OnMinionGroupMemberDiscoveredGlobal += onMinionGroupMemberDiscoveredGlobal;
                MinionHooks.OnMinionGroupMemberLostGlobal += onMinionGroupMemberLostGlobal;

                OnOwnerDiscoveredGlobal?.Invoke(this);
            }
        }

        private void onMinionGroupMemberDiscoveredGlobal(MinionOwnership.MinionGroup minionGroup, CharacterMaster ownerMaster, CharacterMaster memberMaster)
        {
            if (ReferenceEquals(_ownerMaster, ownerMaster))
            {
                onMinionMasterDiscovered(memberMaster);
            }
        }

        private void onMinionGroupMemberLostGlobal(MinionOwnership.MinionGroup minionGroup, CharacterMaster ownerMaster, CharacterMaster memberMaster)
        {
            if (ReferenceEquals(_ownerMaster, ownerMaster))
            {
                onMinionMasterLost(memberMaster);
            }
        }

        private void onMinionMasterDiscovered(CharacterMaster recipientMaster)
        {
            Log.Debug($"Minion discovered: {recipientMaster}, granting item(s)");

            if (recipientMaster && recipientMaster.inventory)
            {
                for (ItemIndex itemIndex = 0; (int)itemIndex < ItemCatalog.itemCount; itemIndex++)
                {
                    recipientMaster.inventory.GiveItemPermanent(itemIndex, _minionMirrorInventory.CalculateEffectiveItemStacks(itemIndex));
                }
            }
        }

        private void onMinionMasterLost(CharacterMaster recipientMaster)
        {
            Log.Debug($"Minion lost: {recipientMaster}, removing item(s)");

            if (recipientMaster && recipientMaster.inventory)
            {
                for (ItemIndex itemIndex = 0; (int)itemIndex < ItemCatalog.itemCount; itemIndex++)
                {
                    recipientMaster.inventory.RemoveItemPermanent(itemIndex, _minionMirrorInventory.CalculateEffectiveItemStacks(itemIndex));
                }
            }
        }

        private void onMinionMirrorInventoryChanged()
        {
            using var _ = ListPool<Inventory>.RentCollection(out List<Inventory> minionInventories);

            if (_ownerMaster)
            {
                MinionOwnership.MinionGroup ownerMinionGroup = MinionOwnership.MinionGroup.FindGroup(_ownerMasterNetworkId);
                if (ownerMinionGroup != null)
                {
                    ListUtils.EnsureCapacity(minionInventories, ownerMinionGroup.memberCount);

                    for (int i = 0; i < ownerMinionGroup.memberCount; i++)
                    {
                        MinionOwnership member = ownerMinionGroup.members[i];
                        if (member && member.TryGetComponent(out CharacterMaster minionMaster) && minionMaster.inventory)
                        {
                            minionInventories.Add(minionMaster.inventory);
                        }
                    }
                }
            }

            for (ItemIndex itemIndex = 0; (int)itemIndex < ItemCatalog.itemCount; itemIndex++)
            {
                ref int prevStack = ref _previousItemStacks[(int)itemIndex];
                int currentStack = _minionMirrorInventory.CalculateEffectiveItemStacks(itemIndex);
                if (prevStack != currentStack)
                {
                    foreach (Inventory minionInventory in minionInventories)
                    {
                        minionInventory.GiveItemPermanent(itemIndex, currentStack - prevStack);
                    }
                }

                prevStack = currentStack;
            }
        }

        [Server]
        public void GiveItemToMinionsServer(Vector3 itemOrigin, ItemIndex itemIndex, int count = 1)
        {
            _minionMirrorInventory.GiveItemPermanent(itemIndex, count);

            MinionOwnership.MinionGroup ownerMinionGroup = MinionOwnership.MinionGroup.FindGroup(_ownerMasterNetworkId);
            if (ownerMinionGroup != null)
            {
                int searchStartIndex = 0;
                float delay = 0f;

                for (int i = 0; i < ownerMinionGroup.memberCount; i++)
                {
                    MinionOwnership member = ownerMinionGroup.members[i];
                    if (member && member.TryGetComponent(out CharacterMaster minionMaster))
                    {
                        ItemGrantEffectStep itemGrantEffect = new ItemGrantEffectStep(minionMaster,
                                                                                      itemIndex,
                                                                                      1f,
                                                                                      itemOrigin,
                                                                                      Run.FixedTimeStamp.now + delay);

                        int index = _itemGrantEffectsQueue.BinarySearch(searchStartIndex, _itemGrantEffectsQueue.Count - searchStartIndex, itemGrantEffect, new ItemGrantEffectStep.StartTimeComparer());
                        if (index < 0)
                        {
                            index = ~index;
                        }

                        _itemGrantEffectsQueue.Insert(index, itemGrantEffect);

                        // We know there can't be any element that will be put before this element in the list since the start time will only increase from here
                        searchStartIndex = index + 1;

                        delay += 0.2f;
                    }
                }
            }
        }

        public static QualityDuplicatorMinionInventoryController FindMinionInventoryController(CharacterMaster ownerMaster)
        {
            if (!ReferenceEquals(ownerMaster, null))
            {
                foreach (QualityDuplicatorMinionInventoryController minionInveneoryController in InstanceTracker.GetInstancesList<QualityDuplicatorMinionInventoryController>())
                {
                    if (ReferenceEquals(minionInveneoryController._ownerMaster, ownerMaster))
                    {
                        return minionInveneoryController;
                    }
                }
            }

            return null;
        }

        public static QualityDuplicatorMinionInventoryController EnsureMinionInventoryControllerServer(CharacterMaster ownerMaster)
        {
            if (!ownerMaster)
            {
                return null;
            }

            QualityDuplicatorMinionInventoryController minionInventoryController = FindMinionInventoryController(ownerMaster);
            if (ReferenceEquals(minionInventoryController, null))
            {
                GameObject minionInventoryControllerObj = Instantiate(ItemQualitiesContent.NetworkedPrefabs.QualityDuplicatorMinionInventory);

                minionInventoryController = minionInventoryControllerObj.GetComponent<QualityDuplicatorMinionInventoryController>();

                minionInventoryControllerObj.GetComponent<GenericOwnership>().ownerObject = ownerMaster.gameObject;

                NetworkServer.Spawn(minionInventoryControllerObj);
            }

            return minionInventoryController;
        }
    }
}
