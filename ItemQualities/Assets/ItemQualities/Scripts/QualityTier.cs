using System.Runtime.CompilerServices;

namespace ItemQualities
{
    public enum QualityTier
    {
        None = -1,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Count
    }
}

namespace ItemQualities.Serialization
{
    internal partial class SerializationExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(this SerializerContext context, QualityTier qualityTier)
        {
            context.Writer.Write((byte)(qualityTier + 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static QualityTier ReadQualityTier(this DeserializerContext context)
        {
            return (QualityTier)context.Reader.ReadByte() - 1;
        }
    }
}
