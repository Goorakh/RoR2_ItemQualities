using ItemQualities.Serialization;
using System;

namespace ItemQualities
{
    public struct StoredInteractableInfo : IEquatable<StoredInteractableInfo>, IBinarySerializable
    {
        public static readonly StoredInteractableInfo None = new StoredInteractableInfo { InteractableIndex = -1 };

        public int InteractableIndex;

        public int UpgradeValue;

        public override readonly bool Equals(object obj)
        {
            return obj is StoredInteractableInfo info && Equals(info);
        }

        readonly bool IEquatable<StoredInteractableInfo>.Equals(StoredInteractableInfo other)
        {
            return Equals(other);
        }

        public readonly bool Equals(in StoredInteractableInfo other)
        {
            return InteractableIndex == other.InteractableIndex &&
                   UpgradeValue == other.UpgradeValue;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(InteractableIndex, UpgradeValue);
        }

        public override readonly string ToString()
        {
            return $"{InteractableCatalog.GetInteractableDef(InteractableIndex)?.Name ?? "None"} (T{UpgradeValue + 1})";
        }

        readonly void IBinarySerializable.Serialize(SerializerContext context)
        {
            Serialize(context);
        }

        internal readonly void Serialize(SerializerContext context)
        {
            context.WritePackedIndex32(InteractableIndex);
            bool hasInteractableIndex = InteractableIndex != -1;
            if (hasInteractableIndex)
            {
                context.WritePackedUInt32((uint)UpgradeValue);
            }
        }

        void IBinarySerializable.Deserialize(DeserializerContext context)
        {
            Deserialize(context);
        }

        internal void Deserialize(DeserializerContext context)
        {
            InteractableIndex = context.ReadPackedIndex32();
            bool hasInteractableIndex = InteractableIndex != -1;
            UpgradeValue = hasInteractableIndex ? (int)context.ReadPackedUInt32() : 0;
        }

        public static bool operator ==(in StoredInteractableInfo left, in StoredInteractableInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in StoredInteractableInfo left, in StoredInteractableInfo right)
        {
            return !left.Equals(right);
        }
    }
}
