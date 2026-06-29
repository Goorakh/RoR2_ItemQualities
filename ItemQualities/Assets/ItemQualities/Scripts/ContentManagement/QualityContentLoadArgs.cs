using HG;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ItemQualities.ContentManagement
{
    public sealed class QualityContentLoadArgs
    {
        private readonly List<ItemQualityGroup> _itemQualityGroups;
        private readonly List<EquipmentQualityGroup> _equipmentQualityGroups;
        private readonly List<BuffQualityGroup> _buffQualityGroups;

        public ReadableProgress<float> ProgressReceiver { get; }

        internal QualityContentLoadArgs(List<ItemQualityGroup> itemQualityGroups, List<EquipmentQualityGroup> equipmentQualityGroups, List<BuffQualityGroup> buffQualityGroups, ReadableProgress<float> progressReceiver)
        {
            _itemQualityGroups = itemQualityGroups;
            _equipmentQualityGroups = equipmentQualityGroups;
            _buffQualityGroups = buffQualityGroups;

            ProgressReceiver = progressReceiver;
        }

        public ItemQualityGroup CreateItemQualityGroup(ItemDef baseItem)
        {
            if (!baseItem)
                throw new ArgumentNullException(nameof(baseItem));

            ItemQualityGroup itemQualityGroup = ScriptableObject.CreateInstance<ItemQualityGroup>();
            itemQualityGroup.name = "ig" + baseItem.name;
            itemQualityGroup.BaseItem = baseItem;

            _itemQualityGroups.Add(itemQualityGroup);

            return itemQualityGroup;
        }

        public ItemQualityGroup CreateItemQualityGroup(AssetReferenceT<ItemDef> baseItemReference)
        {
            if (baseItemReference == null || !baseItemReference.RuntimeKeyIsValid())
                throw new ArgumentException("Base item reference must be a valid asset key", nameof(baseItemReference));

            ItemQualityGroup itemQualityGroup = ScriptableObject.CreateInstance<ItemQualityGroup>();
            itemQualityGroup.name = "ig" + baseItemReference.RuntimeKey;
            itemQualityGroup.BaseItemReference = baseItemReference;

            _itemQualityGroups.Add(itemQualityGroup);

            return itemQualityGroup;
        }

        public EquipmentQualityGroup CreateEquipmentQualityGroup(EquipmentDef baseEquipment)
        {
            if (!baseEquipment)
                throw new ArgumentNullException(nameof(baseEquipment));

            EquipmentQualityGroup equipmentQualityGroup = ScriptableObject.CreateInstance<EquipmentQualityGroup>();
            equipmentQualityGroup.name = "eg" + baseEquipment.name;
            equipmentQualityGroup.BaseEquipment = baseEquipment;

            _equipmentQualityGroups.Add(equipmentQualityGroup);

            return equipmentQualityGroup;
        }

        public EquipmentQualityGroup CreateEquipmentQualityGroup(AssetReferenceT<EquipmentDef> baseEquipmentReference)
        {
            if (baseEquipmentReference == null || !baseEquipmentReference.RuntimeKeyIsValid())
                throw new ArgumentException("Base equipment reference must be a valid asset key", nameof(baseEquipmentReference));

            EquipmentQualityGroup equipmentQualityGroup = ScriptableObject.CreateInstance<EquipmentQualityGroup>();
            equipmentQualityGroup.name = "eg" + baseEquipmentReference.RuntimeKey;
            equipmentQualityGroup.BaseEquipmentReference = baseEquipmentReference;

            _equipmentQualityGroups.Add(equipmentQualityGroup);

            return equipmentQualityGroup;
        }

        public BuffQualityGroup CreateBuffQualityGroup(BuffDef baseBuff)
        {
            if (!baseBuff)
                throw new ArgumentNullException(nameof(baseBuff));

            BuffQualityGroup buffQualityGroup = ScriptableObject.CreateInstance<BuffQualityGroup>();
            buffQualityGroup.name = "bg" + baseBuff.name;
            buffQualityGroup.BaseBuff = baseBuff;

            _buffQualityGroups.Add(buffQualityGroup);

            return buffQualityGroup;
        }

        public BuffQualityGroup CreateBuffQualityGroup(AssetReferenceT<BuffDef> baseBuffReference)
        {
            if (baseBuffReference == null || !baseBuffReference.RuntimeKeyIsValid())
                throw new ArgumentException("Base buff reference must be a valid asset key", nameof(baseBuffReference));

            BuffQualityGroup buffQualityGroup = ScriptableObject.CreateInstance<BuffQualityGroup>();
            buffQualityGroup.name = "bg" + baseBuffReference.RuntimeKey;
            buffQualityGroup.BaseBuffReference = baseBuffReference;

            _buffQualityGroups.Add(buffQualityGroup);

            return buffQualityGroup;
        }
    }
}
