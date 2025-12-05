using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class DronesDropDynamiteQualityItemBehavior : MonoBehaviour
    {
        CharacterBody _body;

        readonly HashSet<MinionInfo> _trackedMinions = new HashSet<MinionInfo>();

        void Awake()
        {
            _body = GetComponent<CharacterBody>();
        }

        void OnEnable()
        {
            MinionOwnership.onMinionOwnerChangedGlobal += onMinionOwnerChangedGlobal;

            if (_body.master)
            {
                MinionOwnership.MinionGroup minionGroup = MinionOwnership.MinionGroup.FindGroup(_body.master.netId);
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

        void OnDisable()
        {
            MinionOwnership.onMinionOwnerChangedGlobal -= onMinionOwnerChangedGlobal;

            foreach (MinionInfo minion in _trackedMinions)
            {
                onMinionExit(minion);
            }

            _trackedMinions.Clear();
        }

        void onMinionOwnerChangedGlobal(MinionOwnership minionOwnership)
        {
            MinionInfo minionInfo = new MinionInfo(minionOwnership);
            if (_body.master && minionOwnership.ownerMaster == _body.master)
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

        void onMinionEnter(MinionInfo minion)
        {
            if (minion.Inventory)
            {
                minion.Inventory.GiveItemPermanent(ItemQualitiesContent.Items.DronesDropDynamiteQualityDroneItem);
            }
        }

        void onMinionExit(MinionInfo minion)
        {
            if (minion.Inventory)
            {
                minion.Inventory.RemoveItemPermanent(ItemQualitiesContent.Items.DronesDropDynamiteQualityDroneItem);
            }
        }

        sealed class MinionInfo : IEquatable<MinionInfo>
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
