namespace ItemQualities.Serialization
{
    internal interface IBinarySerializable
    {
        void Serialize(SerializerContext context);

        void Deserialize(DeserializerContext context);
    }
}
