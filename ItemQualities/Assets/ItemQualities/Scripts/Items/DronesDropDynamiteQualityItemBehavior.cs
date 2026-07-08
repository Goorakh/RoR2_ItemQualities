using RoR2;
using System;
using System.Collections.Generic;

namespace ItemQualities.Items
{
    public sealed class DronesDropDynamiteQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup()
        {
            return ItemQualitiesContent.ItemQualityGroups.DronesDropDynamite;
        }

        private readonly HashSet<MinionInfo> _trackedMinions = new HashSet<MinionInfo>();

        private void OnEnable()
        {
            MinionOwnership.onMinionOwnerChangedGlobal += onMinionOwnerChangedGlobal;

            if (Body.master)
            {
                MinionOwnership.MinionGroup minionGroup = MinionOwnership.MinionGroup.FindGroup(Body.master.netId);
                if (minionGroup != null)
                {
                    _trackedMinions.EnsureCapacity(minionGroup.memberCount);

                    for (int i = 0; i < minionGroup.memberCount; i++)
                    {
                        MinionOwnership minion = minionGroup.members[i];
                        if (minion)
                        {
                            MinionInfo minionInfo = new MinionInfo(minion);
                            if (_trackedMinions.Add(minionInfo))
                            {
                                onMinionEnter(minionInfo);
                            }
                        }
                    }
                }
            }
        }

        private void OnDisable()
        {
            MinionOwnership.onMinionOwnerChangedGlobal -= onMinionOwnerChangedGlobal;

            foreach (MinionInfo minion in _trackedMinions)
            {
                onMinionExit(minion);
            }

            _trackedMinions.Clear();
        }

        private void onMinionOwnerChangedGlobal(MinionOwnership minionOwnership)
        {
            MinionInfo minionInfo = new MinionInfo(minionOwnership);
            if (Body.master && minionOwnership.ownerMaster == Body.master)
            {
                if (_trackedMinions.Add(minionInfo))
                {
                    onMinionEnter(minionInfo);
                }
            }
            else
            {
                if (_trackedMinions.Remove(minionInfo))
                {
                    onMinionExit(minionInfo);
                }
            }
        }

        private void onMinionEnter(MinionInfo minion)
        {
            if (minion.Inventory)
            {
                minion.Inventory.GiveItemPermanent(ItemQualitiesContent.Items.DronesDropDynamiteQualityDroneItem);
            }
        }

        private void onMinionExit(MinionInfo minion)
        {
            if (minion.Inventory)
            {
                minion.Inventory.RemoveItemPermanent(ItemQualitiesContent.Items.DronesDropDynamiteQualityDroneItem);
            }
        }

        private sealed class MinionInfo : IEquatable<MinionInfo>
        {
            public MinionOwnership Ownership { get; }

            public CharacterMaster Master { get; }

            public Inventory Inventory { get; }

            public MinionInfo(MinionOwnership minion)
            {
                Ownership = minion;
                Master = Ownership ? Ownership.GetComponent<CharacterMaster>() : null;
                Inventory = Master ? Master.inventory : (Ownership ? Ownership.GetComponent<Inventory>() : null);
            }

            public bool Equals(MinionInfo other)
            {
                return Ownership == other.Ownership;
            }
        }
    }
}
