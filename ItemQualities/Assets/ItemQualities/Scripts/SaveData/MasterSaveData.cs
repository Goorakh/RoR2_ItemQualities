using HG;
using ItemQualities.Serialization;
using ItemQualities.Utilities.Extensions;
using RoR2;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ItemQualities.SaveData
{
    internal sealed class MasterSaveData : IBinarySerializable
    {
        public MasterIdentifier Identifier { get; }

        public float SteakBonus { get; private set; }

        public int SpeedOnPickupBonus { get; private set; }

        public int BossDamageBonusTicks { get; private set; }

        public uint QualityInfusionBonus { get; private set; }

        private StoredInteractableInfo _cardStoredInteractableInfo;
        public ref readonly StoredInteractableInfo CardStoredInteractableInfo => ref _cardStoredInteractableInfo;

        private ParryStoredProjectileInfo _parryStoredProjectileInfo;
        public ref readonly ParryStoredProjectileInfo ParryStoredProjectileInfo => ref _parryStoredProjectileInfo;

        private readonly List<ItemIndex> _upgradeItemIndices = new List<ItemIndex>();
        public readonly ReadOnlyCollection<ItemIndex> UpgradeItemIndices;

        public MasterSaveData()
        {
            Identifier = new MasterIdentifier();
            UpgradeItemIndices = _upgradeItemIndices.AsReadOnly();
        }

        public MasterSaveData(CharacterMaster master)
        {
            Identifier = MasterIdentifier.FromMaster(master);

            if (master.TryGetComponentCached(out CharacterMasterExtraStatsTracker masterExtraStats))
            {
                SteakBonus = masterExtraStats.SteakBonus;
                SpeedOnPickupBonus = masterExtraStats.SpeedOnPickupBonus;
                BossDamageBonusTicks = masterExtraStats.BossDamageBonusTicks;
                QualityInfusionBonus = masterExtraStats.QualityInfusionBonus;
                _cardStoredInteractableInfo = masterExtraStats.CardStoredInteractableInfo;
                _parryStoredProjectileInfo = masterExtraStats.ParryStoredProjectileInfo;
                masterExtraStats.GetItemUpgradeIndices(_upgradeItemIndices);
            }
        }

        public void Serialize(SerializerContext context)
        {
            Identifier.Serialize(context);
            context.Writer.Write(SteakBonus);
            context.WritePackedUInt32((uint)SpeedOnPickupBonus);
            context.WritePackedUInt32((uint)BossDamageBonusTicks);
            context.WritePackedUInt32(QualityInfusionBonus);
            _cardStoredInteractableInfo.Serialize(context);
            _parryStoredProjectileInfo.Serialize(context);

            context.WritePackedUInt32((uint)_upgradeItemIndices.Count);
            foreach (ItemIndex itemIndex in _upgradeItemIndices)
            {
                context.WritePackedIndex32((int)itemIndex);
            }
        }

        public void Deserialize(DeserializerContext context)
        {
            Identifier.Deserialize(context);
            SteakBonus = context.Reader.ReadSingle();
            SpeedOnPickupBonus = (int)context.ReadPackedUInt32();
            BossDamageBonusTicks = (int)context.ReadPackedUInt32();

            if (context.SerializedVersion > 1)
            {
                QualityInfusionBonus = context.ReadPackedUInt32();
            }

            _cardStoredInteractableInfo.Deserialize(context);

            if (context.SerializedVersion > 0)
            {
                _parryStoredProjectileInfo.Deserialize(context);
            }

            int itemUpgradeCount = (int)context.ReadPackedUInt32();
            ListUtils.EnsureCapacity(_upgradeItemIndices, itemUpgradeCount);
            for (int i = 0; i < itemUpgradeCount; i++)
            {
                _upgradeItemIndices.Add((ItemIndex)context.ReadPackedIndex32());
            }
        }
    }
}
