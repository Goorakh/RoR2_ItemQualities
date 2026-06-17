using ItemQualities.Serialization;
using System;

namespace ItemQualities.SaveData
{
    internal sealed class SaveContainer : IBinarySerializable
    {
        public MasterSaveData[] Masters { get; set; } = Array.Empty<MasterSaveData>();

        public void Serialize(SerializerContext context)
        {
            context.WriteArray(Masters);
        }

        public void Deserialize(DeserializerContext context)
        {
            Masters = context.ReadArray<MasterSaveData>();
        }
    }
}
