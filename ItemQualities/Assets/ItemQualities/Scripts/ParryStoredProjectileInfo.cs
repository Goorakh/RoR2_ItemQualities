using ItemQualities.Serialization;
using RoR2;
using System;

namespace ItemQualities
{
    [Serializable]
    public struct ParryStoredProjectileInfo : IEquatable<ParryStoredProjectileInfo>, IBinarySerializable
    {
        public static readonly ParryStoredProjectileInfo None = new ParryStoredProjectileInfo
        {
            ProjectileIndex = -1,
            Damage = 0f,
            Crit = false,
            Force = 0f,
            AttackerBodyIndex = BodyIndex.None,
            QualityTier = QualityTier.None,
        };

        public int ProjectileIndex;
        public float Damage;
        public bool Crit;
        public float Force;
        public BodyIndex AttackerBodyIndex;
        public QualityTier QualityTier;

        readonly void IBinarySerializable.Serialize(SerializerContext context)
        {
            Serialize(context);
        }

        internal readonly void Serialize(SerializerContext context)
        {
            context.WritePackedIndex32(ProjectileIndex);
            context.Writer.Write(Damage);
            context.WriteSharedBit(Crit);
            context.Writer.Write(Force);
            context.WritePackedIndex32((int)AttackerBodyIndex);
            context.Write(QualityTier);
        }

        void IBinarySerializable.Deserialize(DeserializerContext context)
        {
            Deserialize(context);
        }

        internal void Deserialize(DeserializerContext context)
        {
            ProjectileIndex = context.ReadPackedIndex32();
            Damage = context.Reader.ReadSingle();
            Crit = context.ReadSharedBit();
            Force = context.Reader.ReadSingle();
            AttackerBodyIndex = (BodyIndex)context.ReadPackedIndex32();
            QualityTier = context.ReadQualityTier();
        }

        readonly bool IEquatable<ParryStoredProjectileInfo>.Equals(ParryStoredProjectileInfo other)
        {
            return Equals(other);
        }

        public override readonly bool Equals(object obj)
        {
            return obj is ParryStoredProjectileInfo info && Equals(info);
        }

        public readonly bool Equals(in ParryStoredProjectileInfo other)
        {
            return ProjectileIndex == other.ProjectileIndex &&
                   Damage == other.Damage &&
                   Crit == other.Crit &&
                   Force == other.Force &&
                   AttackerBodyIndex == other.AttackerBodyIndex;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(ProjectileIndex, Damage, Crit, Force, AttackerBodyIndex);
        }

        public static bool operator ==(in ParryStoredProjectileInfo left, in ParryStoredProjectileInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in ParryStoredProjectileInfo left, in ParryStoredProjectileInfo right)
        {
            return !left.Equals(right);
        }
    }
}
