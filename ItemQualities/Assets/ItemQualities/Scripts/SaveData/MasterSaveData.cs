using HG;
using ItemQualities.Serialization;
using RoR2;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ItemQualities.SaveData
{
    internal sealed class MasterSaveData : IBinarySerializable
    {
        public MasterIdentifier Identifier { get; } = new MasterIdentifier();

        public float SteakBonus { get; private set; }

        public int SpeedOnPickupBonus { get; private set; }

        public int BossDamageBonusTicks { get; private set; }

        private StoredInteractableInfo _cardStoredInteractableInfo;
        public ref readonly StoredInteractableInfo CardStoredInteractableInfo => ref _cardStoredInteractableInfo;

        private readonly List<ItemIndex> _upgradeItemIndices = new List<ItemIndex>();
        public readonly ReadOnlyCollection<ItemIndex> UpgradeItemIndices;

        public MasterSaveData()
        {
            UpgradeItemIndices = _upgradeItemIndices.AsReadOnly();
        }

        public void Serialize(WriterContext context)
        {
            Identifier.Serialize(context);
            context.Writer.Write(SteakBonus);
            context.WritePackedUInt32((uint)SpeedOnPickupBonus);
            context.WritePackedUInt32((uint)BossDamageBonusTicks);
            _cardStoredInteractableInfo.Serialize(context);

            context.WritePackedUInt32((uint)_upgradeItemIndices.Count);
            foreach (ItemIndex itemIndex in _upgradeItemIndices)
            {
                context.WritePackedIndex32((int)itemIndex);
            }
        }

        public void Deserialize(ReaderContext context)
        {
            Identifier.Deserialize(context);
            SteakBonus = context.Reader.ReadSingle();
            SpeedOnPickupBonus = (int)context.ReadPackedUInt32();
            BossDamageBonusTicks = (int)context.ReadPackedUInt32();
            _cardStoredInteractableInfo.Deserialize(context);

            int itemUpgradeCount = (int)context.ReadPackedUInt32();
            ListUtils.EnsureCapacity(_upgradeItemIndices, itemUpgradeCount);
            for (int i = 0; i < itemUpgradeCount; i++)
            {
                _upgradeItemIndices.Add((ItemIndex)context.ReadPackedIndex32());
            }
        }
    }
}
