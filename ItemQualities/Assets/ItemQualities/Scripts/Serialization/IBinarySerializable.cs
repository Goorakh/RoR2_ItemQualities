namespace ItemQualities.Serialization
{
    internal interface IBinarySerializable
    {
        void Serialize(WriterContext context);

        void Deserialize(ReaderContext context);
    }
}
