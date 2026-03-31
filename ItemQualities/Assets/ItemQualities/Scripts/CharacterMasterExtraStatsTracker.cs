using HG;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    public sealed class CharacterMasterExtraStatsTracker : NetworkBehaviour
    {
        public static readonly float ItemUpgradeDelay = 0.4f;

        [SystemInitializer(typeof(MasterCatalog))]
        static void Init()
        {
            foreach (CharacterMaster master in MasterCatalog.allMasters)
            {
                if (master)
                {
                    master.gameObject.EnsureComponent<CharacterMasterExtraStatsTracker>();
                }
            }
        }

        CharacterMaster _master;

        CharacterBody _cachedBody;
        CharacterBodyExtraStatsTracker _bodyExtraStatsComponent;

        [SyncVar(hook = nameof(hookSetSteakBonus))]
        public float SteakBonus;

        [SyncVar(hook = nameof(hookSetSpeedOnPickupBonus))]
        public int SpeedOnPickupBonus;

        [SyncVar(hook = nameof(hookSetBossDamageBonusTicks))]
        public int BossDamageBonusTicks;

        [SyncVar]
        public StoredInteractableInfo CardStoredInteractableInfo = StoredInteractableInfo.None;

        readonly SyncListUInt _upgradeItemIndices = new SyncListUInt();

        List<PendingItemUpgrade> _pendingItemUpgrades;
        struct PendingItemUpgrade
        {
            public readonly ItemIndex UpgradeItemIndex;

            public Run.FixedTimeStamp TimeStamp;

            public PendingItemUpgrade(ItemIndex upgradeItemIndex, Run.FixedTimeStamp timeStamp)
            {
                UpgradeItemIndex = upgradeItemIndex;
                TimeStamp = timeStamp;
            }
        }

        int _stageIncomingDamageInstanceCountServer;
        public int StageDamageInstancesTakenCount => _stageIncomingDamageInstanceCountServer;

        public event Action<CharacterMasterExtraStatsTracker> OnStageDamageInstancesTakenCountChangedServer;
        public event Action<CharacterMasterExtraStatsTracker> OnBossDamageBonusTicksChanged;

        void Awake()
        {
            _master = GetComponent<CharacterMaster>();

            ComponentCache.Add(gameObject, this);

            if (NetworkServer.active)
            {
                _pendingItemUpgrades = ListPool<PendingItemUpgrade>.RentCollection();
            }
        }

        void OnDestroy()
        {
            ComponentCache.Remove(gameObject, this);

            if (_pendingItemUpgrades != null)
            {
                _pendingItemUpgrades = ListPool<PendingItemUpgrade>.ReturnCollection(_pendingItemUpgrades);
            }
        }

        void OnEnable()
        {
            _master.onBodyStart += setBody;
            _master.onBodyDestroyed += setBody;

            if (_master.inventory)
            {
                _master.inventory.onInventoryChanged += onInventoryChanged;
            }

            Stage.onServerStageBegin += onServerStageBegin;

            setBody(_master.GetBody());
        }

        void OnDisable()
        {
            _master.onBodyStart -= setBody;
            _master.onBodyDestroyed -= setBody;

            if (_master.inventory)
            {
                _master.inventory.onInventoryChanged -= onInventoryChanged;
            }

            Stage.onServerStageBegin -= onServerStageBegin;

            setBody(null);
        }

        void setBody(CharacterBody body)
        {
            if (_cachedBody == body)
                return;

            if (_bodyExtraStatsComponent)
            {
                _bodyExtraStatsComponent.OnIncomingDamageServer -= onIncomingDamageServer;
            }

            _cachedBody = body;
            _bodyExtraStatsComponent = body ? body.GetComponentCached<CharacterBodyExtraStatsTracker>() : null;

            if (_bodyExtraStatsComponent)
            {
                _bodyExtraStatsComponent.OnIncomingDamageServer += onIncomingDamageServer;
            }
        }

        void onInventoryChanged()
        {
            if (NetworkServer.active)
            {
                checkAllItemQualityUpgrades();
            }
        }

        [Server]
        bool checkAllItemQualityUpgrades()
        {
            bool hasAnyPendingUpgrade = false;

            foreach (uint upgradedItemIndexInt in _upgradeItemIndices)
            {
                ItemIndex upgradedItemIndex = (ItemIndex)upgradedItemIndexInt;
                if (checkItemQualityUpgrade(upgradedItemIndex))
                {
                    hasAnyPendingUpgrade = true;
                }
            }

            return hasAnyPendingUpgrade;
        }

        [Server]
        bool checkItemQualityUpgrade(ItemIndex upgradedItemIndex)
        {
            if (_pendingItemUpgrades.Any(p => p.UpgradeItemIndex == upgradedItemIndex))
                return true;

            ItemQualityGroupIndex itemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(upgradedItemIndex);
            ItemQualityGroup itemGroup = QualityCatalog.GetItemQualityGroup(itemGroupIndex);
            QualityTier upgradeQualityTier = QualityCatalog.GetQualityTier(upgradedItemIndex);

            bool hasAnyUpgradableItem = false;

            for (QualityTier qualityTier = QualityTier.None; qualityTier < upgradeQualityTier; qualityTier++)
            {
                if (getUpgradeItemTransformation(itemGroup.GetItemIndex(qualityTier), upgradedItemIndex).CanTake(_master.inventory, out _))
                {
                    hasAnyUpgradableItem = true;
                }
            }

            if (hasAnyUpgradableItem)
            {
                _pendingItemUpgrades.Add(new PendingItemUpgrade(upgradedItemIndex, Run.FixedTimeStamp.now + ItemUpgradeDelay));
            }

            return hasAnyUpgradableItem;
        }

        static Inventory.ItemTransformation getUpgradeItemTransformation(ItemIndex originalItemIndex, ItemIndex upgradedItemIndex)
        {
            QualityTier upgradeQualityTier = QualityCatalog.GetQualityTier(upgradedItemIndex);

            ItemTransformationTypeIndex upgradeTransformationType = (ItemTransformationTypeIndex)CustomTransformationTypes.QualityUpgradeUncommon + (int)upgradeQualityTier;

            return new Inventory.ItemTransformation
            {
                originalItemIndex = originalItemIndex,
                newItemIndex = upgradedItemIndex,
                minToTransform = 1,
                maxToTransform = int.MaxValue,
                transformationType = upgradeTransformationType,
            };
        }

        public bool HasUpgradeForItem(ItemIndex itemIndex)
        {
            QualityTier qualityTier = QualityCatalog.GetQualityTier(itemIndex);
            ItemQualityGroupIndex itemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(itemIndex);

            foreach (uint upgradeItemIndexInt in _upgradeItemIndices)
            {
                ItemIndex upgradeItemIndex = (ItemIndex)upgradeItemIndexInt;
                QualityTier upgradeQualityTier = QualityCatalog.GetQualityTier(upgradeItemIndex);
                ItemQualityGroupIndex upgradeItemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(upgradeItemIndex);

                if (upgradeItemGroupIndex == itemGroupIndex)
                {
                    if (upgradeQualityTier > qualityTier)
                        return true;

                    break;
                }
            }

            return false;
        }

        [Server]
        public ItemIndex TryPermanentUpgradeRandomItemToQualityTier(Xoroshiro128Plus rng, QualityTier targetQualityTier)
        {
            using var _ = ListPool<ItemIndex>.RentCollection(out List<ItemIndex> availableUpgradeItems);

            bool canUpgrade(ItemIndex itemIndex)
            {
                ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                if (itemDef.isConsumed)
                    return false;

                QualityTier qualityTier = QualityCatalog.GetQualityTier(itemIndex);
                if (qualityTier >= targetQualityTier) // Item is already upgraded past the target tier
                    return false;

                ItemQualityGroupIndex itemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(itemIndex);
                if (itemGroupIndex == ItemQualityGroupIndex.Invalid) // Item does not have any qualities
                    return false;

                foreach (uint upgradeItemIndexInt in _upgradeItemIndices)
                {
                    ItemIndex upgradeItemIndex = (ItemIndex)upgradeItemIndexInt;
                    ItemQualityGroupIndex upgradeItemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(upgradeItemIndex);
                    if (upgradeItemGroupIndex == itemGroupIndex)
                    {
                        if (QualityCatalog.GetQualityTier(upgradeItemIndex) >= targetQualityTier) // Item will be upgraded past the target tier
                            return false;

                        break;
                    }
                }

                ItemQualityGroup itemGroup = QualityCatalog.GetItemQualityGroup(itemGroupIndex);
                ItemIndex upgradedItemIndex = itemGroup.GetItemIndex(targetQualityTier);
                if (upgradedItemIndex == ItemIndex.None) // Item does not have the target quality tier
                    return false;

                if (!Run.instance.ruleBook.IsItemRuleEnabled(upgradedItemIndex) || Run.instance.IsItemExpansionLocked(upgradedItemIndex))
                    return false;

                return true;
            }

            using (ListPool<ItemIndex>.RentCollection(out List<ItemIndex> permanentItemIndices))
            {
                _master.inventory.permanentItemStacks.GetNonZeroIndices(permanentItemIndices);
                foreach (ItemIndex itemIndex in permanentItemIndices)
                {
                    if (canUpgrade(itemIndex))
                    {
                        availableUpgradeItems.Add(itemIndex);
                    }
                }
            }

            // If no permanent items can be upgraded, try temps
            if (availableUpgradeItems.Count == 0)
            {
                using (ListPool<ItemIndex>.RentCollection(out List<ItemIndex> temporaryItemIndices))
                {
                    _master.inventory.tempItemsStorage.GetNonZeroIndices(temporaryItemIndices);
                    foreach (ItemIndex itemIndex in temporaryItemIndices)
                    {
                        if (canUpgrade(itemIndex))
                        {
                            availableUpgradeItems.Add(itemIndex);
                        }
                    }
                }
            }

            // If no items from our inventory can be upgraded, fall back on run available items
            if (availableUpgradeItems.Count == 0)
            {
                WeightedSelection<ItemIndex> itemSelection = new WeightedSelection<ItemIndex>();

                void addDropListToSelection(IList<PickupIndex> dropList, float weight)
                {
                    foreach (PickupIndex pickupIndex in dropList)
                    {
                        PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
                        if (pickupDef != null && pickupDef.itemIndex != ItemIndex.None && canUpgrade(pickupDef.itemIndex))
                        {
                            itemSelection.AddChoice(pickupDef.itemIndex, weight);
                        }
                    }
                }

                addDropListToSelection(Run.instance.availableTier1DropList, 1f);
                addDropListToSelection(Run.instance.availableTier2DropList, 0.7f);
                addDropListToSelection(Run.instance.availableTier3DropList, 0.2f);

                addDropListToSelection(Run.instance.availableVoidTier1DropList, 0.4f);
                addDropListToSelection(Run.instance.availableVoidTier2DropList, 0.4f);
                addDropListToSelection(Run.instance.availableVoidTier3DropList, 0.2f);

                addDropListToSelection(Run.instance.availableBossDropList, 0.2f);
                addDropListToSelection(Run.instance.availableVoidBossDropList, 0.1f);

                int itemsToAdd = Mathf.Min(20, itemSelection.Count);
                for (int i = 0; i < itemsToAdd; i++)
                {
                    int choiceIndex = itemSelection.EvaluateToChoiceIndex(rng.nextNormalizedFloat);
                    WeightedSelection<ItemIndex>.ChoiceInfo choiceInfo = itemSelection.GetChoice(choiceIndex);
                    ItemIndex itemIndex = choiceInfo.value;

                    availableUpgradeItems.Add(itemIndex);

                    itemSelection.RemoveChoice(choiceIndex);
                }
            }

            // If we still don't have any upgradable items at this point just give up
            if (availableUpgradeItems.Count == 0)
                return ItemIndex.None;
            
            ItemIndex upgradedItemIndex = QualityCatalog.GetItemIndexOfQuality(rng.NextElementUniform(availableUpgradeItems), targetQualityTier);
            ItemQualityGroupIndex upgradeItemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(upgradedItemIndex);

            bool addedToList = false;
            for (int i = _upgradeItemIndices.Count - 1; i >= 0; i--)
            {
                ItemIndex itemIndex = (ItemIndex)_upgradeItemIndices[i];
                ItemQualityGroupIndex itemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(itemIndex);
                if (itemGroupIndex == upgradeItemGroupIndex)
                {
                    _upgradeItemIndices[i] = (uint)upgradedItemIndex;
                    addedToList = true;
                    break;
                }
            }

            if (!addedToList)
            {
                _upgradeItemIndices.Add((uint)upgradedItemIndex);
            }

            bool hasPendingUpgrade = checkItemQualityUpgrade(upgradedItemIndex);
            if (!hasPendingUpgrade)
            {
                // If there are no current items to upgrade, notify what item was upgraded since it won't show up as a transformation notification
                CharacterMasterNotificationQueue.CustomOverrideInfo upgradeNotificationInfo =
                    new CharacterMasterNotificationQueue.CustomOverrideInfo()
                    .SetDescriptionText("QUALITY_HEALANDREVIVE_PICKUP_NOTIFICATION_UPGRADED_" + targetQualityTier.ToString().ToUpper());

                CharacterMasterNotificationQueue.SendCustomNotification(_master, upgradedItemIndex, upgradeNotificationInfo);
            }

            return upgradedItemIndex;
        }

        void FixedUpdate()
        {
            if (NetworkServer.active)
            {
                for (int i = _pendingItemUpgrades.Count - 1; i >= 0; i--)
                {
                    PendingItemUpgrade pendingUpgrade = _pendingItemUpgrades[i];
                    if (pendingUpgrade.TimeStamp.hasPassed)
                    {
                        ItemQualityGroupIndex itemGroupIndex = QualityCatalog.FindItemQualityGroupIndex(pendingUpgrade.UpgradeItemIndex);
                        ItemQualityGroup itemGroup = QualityCatalog.GetItemQualityGroup(itemGroupIndex);
                        QualityTier upgradeQualityTier = QualityCatalog.GetQualityTier(pendingUpgrade.UpgradeItemIndex);

                        bool upgradedAnyItem = false;

                        for (QualityTier qualityTier = QualityTier.None; qualityTier < upgradeQualityTier; qualityTier++)
                        {
                            Inventory.ItemTransformation upgradeTransformation = getUpgradeItemTransformation(itemGroup.GetItemIndex(qualityTier), pendingUpgrade.UpgradeItemIndex);
                            if (upgradeTransformation.TryTransform(_master.inventory, out _))
                            {
                                upgradedAnyItem = true;
                            }
                        }

                        if (upgradedAnyItem)
                        {
                            _pendingItemUpgrades.RemoveAt(i);
                        }
                        else
                        {
                            pendingUpgrade.TimeStamp = Run.FixedTimeStamp.now + ItemUpgradeDelay;
                            _pendingItemUpgrades[i] = pendingUpgrade;
                        }
                    }
                }
            }
        }

        void onServerStageBegin(Stage stage)
        {
            if (_stageIncomingDamageInstanceCountServer != 0)
            {
                _stageIncomingDamageInstanceCountServer = 0;
                OnStageDamageInstancesTakenCountChangedServer?.Invoke(this);
            }
        }

        void onIncomingDamageServer(DamageInfo damageInfo)
        {
            if (damageInfo.damage > 0f &&
                !damageInfo.delayedDamageSecondHalf &&
                (damageInfo.damageType & DamageType.DoT) == 0 &&
                !damageInfo.IsParried())
            {
                _stageIncomingDamageInstanceCountServer++;
                OnStageDamageInstancesTakenCountChangedServer?.Invoke(this);
            }
        }

        void markBodyStatsDirty()
        {
            if (_cachedBody)
            {
                _cachedBody.MarkAllStatsDirty();
            }
        }

        void hookSetSteakBonus(float steakBonus)
        {
            bool changed = SteakBonus != steakBonus;
            SteakBonus = steakBonus;

            if (changed)
            {
                markBodyStatsDirty();
            }
        }

        void hookSetSpeedOnPickupBonus(int speedOnPickupBonus)
        {
            bool changed = SpeedOnPickupBonus != speedOnPickupBonus;
            SpeedOnPickupBonus = speedOnPickupBonus;

            if (changed)
            {
                markBodyStatsDirty();
            }
        }

        void hookSetBossDamageBonusTicks(int bossDamageBonusTicks)
        {
            bool changed = BossDamageBonusTicks != bossDamageBonusTicks;
            BossDamageBonusTicks = bossDamageBonusTicks;

            if (changed)
            {
                OnBossDamageBonusTicksChanged?.Invoke(this);
            }
        }
    }
}
