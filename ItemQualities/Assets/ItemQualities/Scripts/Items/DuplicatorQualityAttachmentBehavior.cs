using HG;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    [RequireComponent(typeof(NetworkedBodyAttachment))]
    [RequireComponent(typeof(Inventory))]
    public sealed class DuplicatorQualityAttachmentBehavior : NetworkBehaviour, INetworkedBodyAttachmentListener
    {
        Inventory _minionMirrorInventory;

        CharacterMaster _attachedMaster;
        Inventory _attachedBodyInventory;
        CharacterBody _attachedBody;

        readonly List<Inventory> _cachedMinionInventories = new List<Inventory>();

        bool _attachedInventoryChanged;

        public CharacterBody AttachedBody
        {
            get
            {
                return _attachedBody;
            }
            private set
            {
                if (_attachedBody == value)
                    return;

                if (_attachedBody)
                {
                    _attachedBody.onInventoryChanged -= onAttachedBodyInventoryChanged;
                }

                _attachedBody = value;
                _attachedMaster = _attachedBody ? _attachedBody.master : null;
                _attachedBodyInventory = _attachedBody ? _attachedBody.inventory : null;

                if (_attachedBody)
                {
                    _attachedBody.onInventoryChanged += onAttachedBodyInventoryChanged;
                }

                using (new Inventory.InventoryChangeScope(_minionMirrorInventory))
                {
                    clearMinionInventory();

                    if (_attachedBodyInventory)
                    {
                        using (ListPool<ItemIndex>.RentCollection(out List<ItemIndex> nonZeroItemIndices))
                        {
                            _attachedBodyInventory.tempItemsStorage.GetNonZeroIndices(nonZeroItemIndices);
                            foreach (ItemIndex itemIndex in nonZeroItemIndices)
                            {
                                ItemIndex sharedItemIndex = getSharedItemIndex(itemIndex);
                                if (sharedItemIndex != ItemIndex.None)
                                {
                                    _minionMirrorInventory.GiveItemTemp(itemIndex, _attachedBodyInventory.GetTempItemRawValue(itemIndex));
                                }
                            }
                        }
                    }
                }

                for (int i = _cachedMinionInventories.Count - 1; i >= 0; i--)
                {
                    if (_cachedMinionInventories[i] && _cachedMinionInventories[i].TryGetComponent(out MinionOwnership minion))
                    {
                        handleMinionExit(minion);
                    }
                }

                _cachedMinionInventories.Clear();

                if (_attachedMaster)
                {
                    MinionOwnership.MinionGroup minionGroup = MinionOwnership.MinionGroup.FindGroup(_attachedMaster.netId);
                    if (minionGroup != null)
                    {
                        _cachedMinionInventories.EnsureCapacity(minionGroup.memberCount);

                        for (int i = 0; i < minionGroup.memberCount; i++)
                        {
                            MinionOwnership minion = minionGroup.members[i];
                            if (minion)
                            {
                                handleMinionEnter(minion);
                            }
                        }
                    }
                }

                onAttachedBodyInventoryChanged();

                OnAttachedBodyChangedGlobal?.Invoke(this);
            }
        }

        public Inventory MinionMirrorInventory => _minionMirrorInventory;

        public static event Action<DuplicatorQualityAttachmentBehavior> OnAttachedBodyChangedGlobal;

        void Awake()
        {
            _minionMirrorInventory = GetComponent<Inventory>();

            if (NetworkServer.active)
            {
                InventoryHooks.OnTempItemGivenServerGlobal += onTempItemGivenServerGlobal;

                MinionOwnership.onMinionOwnerChangedGlobal += onMinionOwnerChangedGlobal;

                Inventory.ItemTransformation.onItemTransformedServerGlobal += onItemTransformedServerGlobal;
            }
        }

        void OnEnable()
        {
            InstanceTracker.Add(this);
        }

        void OnDisable()
        {
            InstanceTracker.Remove(this);
        }

        void OnDestroy()
        {
            AttachedBody = null;

            InventoryHooks.OnTempItemGivenServerGlobal -= onTempItemGivenServerGlobal;

            MinionOwnership.onMinionOwnerChangedGlobal -= onMinionOwnerChangedGlobal;

            Inventory.ItemTransformation.onItemTransformedServerGlobal -= onItemTransformedServerGlobal;
        }

        void FixedUpdate()
        {
            if (_attachedInventoryChanged)
            {
                _attachedInventoryChanged = false;
                if (NetworkServer.active)
                {
                    refreshItemDecayDuration();
                }
            }
        }

        float calculateMinionItemDecayDuration()
        {
            float baseDecayDuration = _attachedBodyInventory ? _attachedBodyInventory.tempItemsStorage.decayDuration : 0f;
            if (baseDecayDuration <= 0)
                baseDecayDuration = Inventory.baseItemDecayDuration;

            ItemQualityCounts duplicator = default;
            if (_attachedBodyInventory)
            {
                duplicator = _attachedBodyInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Duplicator);
            }

            if (duplicator.TotalQualityCount == 0)
                duplicator.UncommonCount = 1;

            float minionItemDecayFraction = (0.50f * duplicator.UncommonCount) +
                                            (0.75f * duplicator.RareCount) +
                                            (1.00f * duplicator.EpicCount) +
                                            (1.50f * duplicator.LegendaryCount);

            return baseDecayDuration * minionItemDecayFraction;
        }

        [Server]
        void refreshItemDecayDuration()
        {
            float finalDecayDuration = calculateMinionItemDecayDuration();

            _minionMirrorInventory.SetItemDecayDurationServer(finalDecayDuration);

            foreach (Inventory inventory in _cachedMinionInventories)
            {
                if (inventory)
                {
                    inventory.SetItemDecayDurationServer(finalDecayDuration);
                }
            }
        }

        static ItemIndex getSharedItemIndex(ItemIndex itemIndex)
        {
            ItemIndex sharedItemIndex = itemIndex;
            bool canShareItem = Duplicator.ItemShareFilter(sharedItemIndex);

            // For quality items that cannot be shared: Try sharing base item instead
            if (!canShareItem && QualityCatalog.GetQualityTier(sharedItemIndex) != QualityTier.None)
            {
                sharedItemIndex = QualityCatalog.GetItemIndexOfQuality(sharedItemIndex, QualityTier.None);
                canShareItem = Duplicator.ItemShareFilter(sharedItemIndex);
            }

            return canShareItem ? sharedItemIndex : ItemIndex.None;
        }

        [Server]
        void onTempItemGivenServerGlobal(Inventory inventory, ItemIndex itemIndex, int itemCountDiff)
        {
            if (!inventory)
                return;

            if (inventory == _attachedBodyInventory)
            {
                int sharedItemCount = itemCountDiff;
                if (sharedItemCount > 0)
                {
                    float itemDecayValue = inventory.GetTempItemDecayValue(itemIndex);
                    if (itemDecayValue > 0 && itemDecayValue * inventory.tempItemsStorage.decayDuration <= Time.fixedDeltaTime)
                    {
                        // Because temp items are rounded up when converted to an item count there can be situations where, due to floating point precision errors, the raw value could be something like 1.000001, so you would actually get +1 stack for a single update tick since the value would round up to 2.
                        // This combined with the fact that we do not consider the decay value when adding to the minion inventory, can cause a temp item to get duplicated when added to the inventories.

                        Log.Debug($"Prevented duplicate temp item stack {ItemCatalog.GetItemDef(itemIndex).name} (decay={itemDecayValue}) for {Util.GetBestBodyName(AttachedBody.gameObject)} minion inventory");

                        itemDecayValue = 0f;
                        sharedItemCount--;
                    }

                    if (sharedItemCount > 0)
                    {
                        ItemIndex sharedItemIndex = getSharedItemIndex(itemIndex);
                        if (sharedItemIndex != ItemIndex.None)
                        {
                            _minionMirrorInventory.GiveItemTemp(sharedItemIndex, sharedItemCount);

                            foreach (Inventory minionInventory in _cachedMinionInventories)
                            {
                                if (minionInventory)
                                {
                                    minionInventory.GiveItemTemp(sharedItemIndex, sharedItemCount);
                                }
                            }

                            Log.Debug($"Added +{sharedItemCount} {ItemCatalog.GetItemDef(itemIndex).name} ({ItemCatalog.GetItemDef(sharedItemIndex).name}) to minion inventory for {Util.GetBestBodyName(AttachedBody.gameObject)}");
                        }
                    }
                }
            }
        }

        [Server]
        void onItemTransformedServerGlobal(Inventory.ItemTransformation.TryTransformResult transformResult)
        {
            if (transformResult.inventory && transformResult.inventory == _attachedBodyInventory)
            {
                ItemIndex removedItemIndex = transformResult.takenItem.itemIndex;
                float removedTempItemCount = transformResult.takenItem.stackValues.temporaryStacksValue;
                if (removedItemIndex != ItemIndex.None && removedTempItemCount > 0 && Duplicator.ItemShareFilter(removedItemIndex))
                {
                    _minionMirrorInventory.RemoveItemTemp(removedItemIndex, removedTempItemCount);

                    foreach (Inventory minionInventory in _cachedMinionInventories)
                    {
                        if (minionInventory)
                        {
                            minionInventory.RemoveItemTemp(removedItemIndex, removedTempItemCount);
                        }
                    }

                    Log.Debug($"Removed {removedTempItemCount:0.##} {ItemCatalog.GetItemDef(removedItemIndex).name} from minion inventory for {Util.GetBestBodyName(AttachedBody.gameObject)} from item transformation");
                }
            }
        }

        void onAttachedBodyInventoryChanged()
        {
            _attachedInventoryChanged = true;
        }

        [Server]
        void clearMinionInventory()
        {
            using (new Inventory.InventoryChangeScope(_minionMirrorInventory))
            {
                using (ListPool<ItemIndex>.RentCollection(out List<ItemIndex> nonZeroItemIndices))
                {
                    _minionMirrorInventory.tempItemsStorage.GetNonZeroIndices(nonZeroItemIndices);
                    foreach (ItemIndex itemIndex in nonZeroItemIndices)
                    {
                        float itemRawValue = _minionMirrorInventory.GetTempItemRawValue(itemIndex);

                        foreach (Inventory minionInventory in _cachedMinionInventories)
                        {
                            if (minionInventory)
                            {
                                minionInventory.RemoveItemTemp(itemIndex, itemRawValue);
                            }
                        }

                        _minionMirrorInventory.tempItemsStorage.ResetItem(itemIndex);
                    }
                }
            }
        }

        [Server]
        void onMinionOwnerChangedGlobal(MinionOwnership minionOwnership)
        {
            if (_attachedMaster && minionOwnership.ownerMaster == _attachedMaster)
            {
                handleMinionEnter(minionOwnership);
            }
            else
            {
                for (int i = _cachedMinionInventories.Count - 1; i >= 0; i--)
                {
                    Inventory minionInventory = _cachedMinionInventories[i];
                    if (!minionInventory)
                    {
                        _cachedMinionInventories.RemoveAt(i);
                    }
                    else if (minionInventory.gameObject == minionOwnership.gameObject)
                    {
                        handleMinionExit(minionOwnership);
                        break;
                    }
                }
            }
        }

        [Server]
        void handleMinionEnter(MinionOwnership minion)
        {
            if (minion.TryGetComponent(out Inventory minionInventory) && !_cachedMinionInventories.Contains(minionInventory))
            {
                using (new Inventory.InventoryChangeScope(minionInventory))
                {
                    minionInventory.SetItemDecayDurationServer(calculateMinionItemDecayDuration());

                    using (ListPool<ItemIndex>.RentCollection(out List<ItemIndex> nonZeroItemIndices))
                    {
                        _minionMirrorInventory.tempItemsStorage.GetNonZeroIndices(nonZeroItemIndices);
                        foreach (ItemIndex itemIndex in nonZeroItemIndices)
                        {
                            float itemRawValue = _minionMirrorInventory.GetTempItemRawValue(itemIndex);

                            minionInventory.GiveItemTemp(itemIndex, itemRawValue);
                        }
                    }
                }

                _cachedMinionInventories.Add(minionInventory);

                Log.Debug($"Minion enter: {Util.GetBestMasterName(minion.GetComponent<CharacterMaster>())}, on {Util.GetBestBodyName(_attachedBody ? _attachedBody.gameObject : null)}");
            }
        }

        [Server]
        void handleMinionExit(MinionOwnership minion)
        {
            if (minion.TryGetComponent(out Inventory minionInventory) && _cachedMinionInventories.Remove(minionInventory))
            {
                using (new Inventory.InventoryChangeScope(minionInventory))
                {
                    using (ListPool<ItemIndex>.RentCollection(out List<ItemIndex> nonZeroItemIndices))
                    {
                        _minionMirrorInventory.tempItemsStorage.GetNonZeroIndices(nonZeroItemIndices);
                        foreach (ItemIndex itemIndex in nonZeroItemIndices)
                        {
                            float itemRawValue = _minionMirrorInventory.GetTempItemRawValue(itemIndex);

                            minionInventory.RemoveItemTemp(itemIndex, itemRawValue);
                        }
                    }
                }

                Log.Debug($"Minion exit: {Util.GetBestMasterName(minion.GetComponent<CharacterMaster>())}, on {Util.GetBestBodyName(_attachedBody ? _attachedBody.gameObject : null)}");
            }
        }

        void INetworkedBodyAttachmentListener.OnAttachedBodyDiscovered(NetworkedBodyAttachment networkedBodyAttachment, CharacterBody attachedBody)
        {
            AttachedBody = attachedBody;
        }
    }
}
