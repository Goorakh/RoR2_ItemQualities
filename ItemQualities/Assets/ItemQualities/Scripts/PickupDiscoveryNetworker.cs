using HG;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities
{
    [RequireComponent(typeof(NetworkUser))]
    public sealed class PickupDiscoveryNetworker : NetworkBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.NetworkUser.Awake += NetworkUser_Awake;
        }

        static void NetworkUser_Awake(On.RoR2.NetworkUser.orig_Awake orig, NetworkUser self)
        {
            orig(self);
            self.gameObject.EnsureComponent<PickupDiscoveryNetworker>();
        }

        NetworkUser _networkUser;
        bool _hasLocalUser;

        float _retryInitLocalUserTimer = 1f;

        SparseArrayStruct<bool, DiscoveredItemsSparseListImpl> _discoveredItems = new SparseArrayStruct<bool, DiscoveredItemsSparseListImpl>(new DiscoveredItemsSparseListImpl());
        SparseArrayStruct<bool, DiscoveredEquipmentsSparseListImpl> _discoveredEquipments = new SparseArrayStruct<bool, DiscoveredEquipmentsSparseListImpl>(new DiscoveredEquipmentsSparseListImpl());
        SparseArrayStruct<bool, DiscoveredEquipmentsSparseListImpl> _discoveredDrones = new SparseArrayStruct<bool, DiscoveredEquipmentsSparseListImpl>(new DiscoveredEquipmentsSparseListImpl());

        const uint DiscoveredItemsDirtyBit = 1 << 0;
        const uint DiscoveredEquipmentsDirtyBit = 1 << 1;
        const uint DiscoveredDronesDirtyBit = 1 << 2;

        void Awake()
        {
            _networkUser = GetComponent<NetworkUser>();
        }

        void OnEnable()
        {
            tryInitLocalUser();
        }

        void OnDisable()
        {
            cleanupLocalUser();
        }

        void FixedUpdate()
        {
            if (hasAuthority)
            {
                _retryInitLocalUserTimer -= Time.fixedDeltaTime;
                if (_retryInitLocalUserTimer <= 0)
                {
                    _retryInitLocalUserTimer = 1f;
                    tryInitLocalUser();
                }
            }
        }

        void tryInitLocalUser()
        {
            if (!_hasLocalUser && _networkUser && _networkUser.localUser?.userProfile != null)
            {
                List<int> allDiscoveredPickupIndicesInts = new List<int>(PickupCatalog.pickupCount);
                foreach (PickupIndex pickupIndex in PickupCatalog.allPickupIndices)
                {
                    if (_networkUser.localUser.userProfile.HasDiscoveredPickup(pickupIndex))
                    {
                        allDiscoveredPickupIndicesInts.Add(pickupIndex.value);
                    }
                }

                CmdSetDiscoveredPickups(allDiscoveredPickupIndicesInts.ToArray());

                _networkUser.localUser.userProfile.onPickupDiscovered += onPickupDiscoveredAuthority;
                _hasLocalUser = true;
            }
        }

        void cleanupLocalUser()
        {
            if (_hasLocalUser)
            {
                if (_networkUser && _networkUser.localUser?.userProfile != null)
                {
                    _networkUser.localUser.userProfile.onPickupDiscovered -= onPickupDiscoveredAuthority;
                }

                _hasLocalUser = false;
            }
        }

        void onPickupDiscoveredAuthority(PickupIndex pickupIndex)
        {
            CmdSetPickupDiscovered(pickupIndex.value);
        }

        public bool HasDiscoveredPickup(PickupIndex pickupIndex)
        {
            PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
            if (pickupDef != null)
            {
                if (pickupDef.itemIndex != ItemIndex.None)
                {
                    return _discoveredItems.GetValueSafe((SparseIndex)pickupDef.itemIndex);
                }
                else if (pickupDef.equipmentIndex != EquipmentIndex.None)
                {
                    return _discoveredEquipments.GetValueSafe((SparseIndex)pickupDef.equipmentIndex);
                }
                else if (pickupDef.droneIndex != DroneIndex.None)
                {
                    return _discoveredDrones.GetValueSafe((SparseIndex)pickupDef.droneIndex);
                }
            }

            return false;
        }

        [Command]
        void CmdSetPickupDiscovered(int pickupIndexInt)
        {
            PickupIndex pickupIndex = new PickupIndex(pickupIndexInt);
            if (pickupIndex.isValid)
            {
                setPickupDiscovered(pickupIndex, true);
            }
        }

        [Command]
        void CmdSetDiscoveredPickups(int[] pickupIndicesInts)
        {
            foreach (PickupIndex pickupIndex in PickupCatalog.allPickupIndices)
            {
                setPickupDiscovered(pickupIndex, Array.IndexOf(pickupIndicesInts, pickupIndex.value) >= 0);
            }
        }

        [Server]
        void setPickupDiscovered(PickupIndex pickupIndex, bool discovered)
        {
            PickupDef pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
            if (pickupDef != null)
            {
                bool discoveredStatusChanged = false;

                if (pickupDef.itemIndex != ItemIndex.None)
                {
                    if (_discoveredItems.GetValueSafe((SparseIndex)pickupDef.itemIndex) != discovered)
                    {
                        _discoveredItems.SetValue((SparseIndex)pickupDef.itemIndex, discovered);
                        SetDirtyBit(DiscoveredItemsDirtyBit);
                        discoveredStatusChanged = true;
                    }
                }
                else if (pickupDef.equipmentIndex != EquipmentIndex.None)
                {
                    if (_discoveredEquipments.GetValueSafe((SparseIndex)pickupDef.equipmentIndex) != discovered)
                    {
                        _discoveredEquipments.SetValue((SparseIndex)pickupDef.equipmentIndex, discovered);
                        SetDirtyBit(DiscoveredEquipmentsDirtyBit);
                        discoveredStatusChanged = true;
                    }
                }
                else if (pickupDef.droneIndex != DroneIndex.None)
                {
                    if (_discoveredDrones.GetValueSafe((SparseIndex)pickupDef.droneIndex) != discovered)
                    {
                        _discoveredDrones.SetValue((SparseIndex)pickupDef.droneIndex, discovered);
                        SetDirtyBit(DiscoveredDronesDirtyBit);
                        discoveredStatusChanged = true;
                    }
                }

                if (discoveredStatusChanged)
                {
                    Log.Debug($"Discovered {pickupIndex} for {Util.GetBestMasterName(_networkUser.master)}: {discovered}");
                }
            }
        }

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            uint dirtyBits;
            if (initialState)
            {
                dirtyBits = ~0b0U;
            }
            else
            {
                dirtyBits = syncVarDirtyBits;
                writer.WritePackedUInt32(dirtyBits);
            }

            BitMaskSerializer serializer = new BitMaskSerializer();

            bool anythingWritten = false;

            if ((dirtyBits & DiscoveredItemsDirtyBit) != 0)
            {
                _discoveredItems.Serialize(writer, serializer);
                serializer.Flush(writer);

                anythingWritten = true;
            }

            if ((dirtyBits & DiscoveredEquipmentsDirtyBit) != 0)
            {
                _discoveredEquipments.Serialize(writer, serializer);
                serializer.Flush(writer);

                anythingWritten = true;
            }

            if ((dirtyBits & DiscoveredDronesDirtyBit) != 0)
            {
                _discoveredDrones.Serialize(writer, serializer);
                serializer.Flush(writer);

                anythingWritten = true;
            }

            if (anythingWritten)
            {
                Log.Debug($"Serialized discovered pickups ({writer.Position} byte(s))");
            }

            return anythingWritten;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            uint dirtyBits = initialState ? ~0b0U : reader.ReadPackedUInt32();

            BitMaskSerializer serializer = new BitMaskSerializer();

            if ((dirtyBits & DiscoveredItemsDirtyBit) != 0)
            {
                _discoveredItems.Deserialize(reader, serializer);
                serializer.Reset();
            }

            if ((dirtyBits & DiscoveredEquipmentsDirtyBit) != 0)
            {
                _discoveredEquipments.Deserialize(reader, serializer);
                serializer.Reset();
            }

            if ((dirtyBits & DiscoveredDronesDirtyBit) != 0)
            {
                _discoveredDrones.Deserialize(reader, serializer);
            }
        }

        readonly struct DiscoveredItemsSparseListImpl : ISparseArrayImpl<bool>
        {
            public SparseIndex[] AllocDenseArray()
            {
                return ItemCatalog.PerItemBufferPool.Request<SparseIndex>();
            }

            public void FreeDenseArray(SparseIndex[] array)
            {
                ItemCatalog.PerItemBufferPool.Return(ref array);
            }

            public bool[] AllocSparseArray()
            {
                return ItemCatalog.PerItemBufferPool.Request<bool>();
            }

            public void FreeSparseArray(bool[] array)
            {
                ItemCatalog.PerItemBufferPool.Return(ref array);
            }
        }

        readonly struct DiscoveredEquipmentsSparseListImpl : ISparseArrayImpl<bool>
        {
            static readonly FixedSizeArrayPool<SparseIndex> _sparseIndexPool = new FixedSizeArrayPool<SparseIndex>(0);
            static readonly FixedSizeArrayPool<bool> _discoveredValuesPool = new FixedSizeArrayPool<bool>(0);

            public SparseIndex[] AllocDenseArray()
            {
                _sparseIndexPool.lengthOfArrays = EquipmentCatalog.equipmentCount;
                return _sparseIndexPool.Request();
            }

            public void FreeDenseArray(SparseIndex[] array)
            {
                _sparseIndexPool.Return(array);
            }

            public bool[] AllocSparseArray()
            {
                _discoveredValuesPool.lengthOfArrays = EquipmentCatalog.equipmentCount;
                return _discoveredValuesPool.Request();
            }

            public void FreeSparseArray(bool[] array)
            {
                _discoveredValuesPool.Return(array);
            }
        }

        readonly struct DiscoveredDronesSparseListImpl : ISparseArrayImpl<bool>
        {
            static readonly FixedSizeArrayPool<SparseIndex> _sparseIndexPool = new FixedSizeArrayPool<SparseIndex>(0);
            static readonly FixedSizeArrayPool<bool> _discoveredValuesPool = new FixedSizeArrayPool<bool>(0);

            public SparseIndex[] AllocDenseArray()
            {
                _sparseIndexPool.lengthOfArrays = DroneCatalog.droneCount;
                return _sparseIndexPool.Request();
            }

            public void FreeDenseArray(SparseIndex[] array)
            {
                _sparseIndexPool.Return(array);
            }

            public bool[] AllocSparseArray()
            {
                _discoveredValuesPool.lengthOfArrays = DroneCatalog.droneCount;
                return _discoveredValuesPool.Request();
            }

            public void FreeSparseArray(bool[] array)
            {
                _discoveredValuesPool.Return(array);
            }
        }

        sealed class BitMaskSerializer : INetSerializer<bool>
        {
            byte _currentBitMask;
            sbyte _currentBit = -1;

            void INetSerializer<bool>.Serialize(NetworkWriter writer, in bool value)
            {
                if (_currentBit < 0)
                {
                    _currentBit = 0;
                }
                else if (_currentBit >= 8)
                {
                    writer.Write(_currentBitMask);
                    _currentBitMask = 0;
                    _currentBit = 0;
                }

                if (value)
                {
                    _currentBitMask |= (byte)(1 << _currentBit);
                }

                _currentBit++;
            }

            void INetSerializer<bool>.Deserialize(NetworkReader reader, ref bool dest)
            {
                if (_currentBit < 0 || _currentBit >= 8)
                {
                    _currentBitMask = reader.ReadByte();
                    _currentBit = 0;
                }

                dest = (_currentBitMask & (byte)(1 << _currentBit)) != 0;
                _currentBit++;
            }

            public void Reset()
            {
                _currentBitMask = 0;
                _currentBit = -1;
            }

            public void Flush(NetworkWriter writer)
            {
                if (_currentBit > 0)
                {
                    writer.Write(_currentBitMask);
                    _currentBitMask = 0;
                    _currentBit = -1;
                }
            }
        }
    }
}
