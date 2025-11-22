using System;
using System.Runtime.InteropServices;

namespace ItemQualities
{
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct BuffQualityCounts : IEquatable<BuffQualityCounts>
    {
        [FieldOffset(0)]
        fixed int _buffCounts[(int)QualityTier.Count + 1];

        [FieldOffset(0)]
        public int BaseCount;

        [FieldOffset(sizeof(int) * 1)]
        public int UncommonCount;

        [FieldOffset(sizeof(int) * 2)]
        public int RareCount;

        [FieldOffset(sizeof(int) * 3)]
        public int EpicCount;

        [FieldOffset(sizeof(int) * 4)]
        public int LegendaryCount;

        public readonly int TotalCount => BaseCount + UncommonCount + RareCount + EpicCount + LegendaryCount;

        public readonly int TotalQualityCount => UncommonCount + RareCount + EpicCount + LegendaryCount;

        public ref int this[QualityTier qualityTier] => ref _buffCounts[(int)qualityTier + 1];

        public readonly QualityTier HighestQuality
        {
            get
            {
                for (QualityTier qualityTier = QualityTier.Count - 1; qualityTier >= 0; qualityTier--)
                {
                    if (_buffCounts[(int)qualityTier + 1] > 0)
                    {
                        return qualityTier;
                    }
                }

                return QualityTier.None;
            }
        }

        public BuffQualityCounts(int baseCount, int uncommonCount, int rareCount, int epicCount, int legendaryCount)
        {
            BaseCount = Math.Max(0, baseCount);
            UncommonCount = Math.Max(0, uncommonCount);
            RareCount = Math.Max(0, rareCount);
            EpicCount = Math.Max(0, epicCount);
            LegendaryCount = Math.Max(0, legendaryCount);
        }

        public override readonly bool Equals(object obj)
        {
            return obj is BuffQualityCounts counts && Equals(counts);
        }

        readonly bool IEquatable<BuffQualityCounts>.Equals(BuffQualityCounts other)
        {
            return Equals(other);
        }

        public readonly bool Equals(in BuffQualityCounts other)
        {
            return BaseCount == other.BaseCount &&
                   UncommonCount == other.UncommonCount &&
                   RareCount == other.RareCount &&
                   EpicCount == other.EpicCount &&
                   LegendaryCount == other.LegendaryCount;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(BaseCount, UncommonCount, RareCount, EpicCount, LegendaryCount);
        }

        public override readonly string ToString()
        {
            return $"Normal={BaseCount}, Uncommon={UncommonCount}, Rare={RareCount}, Epic={EpicCount}, Legendary={LegendaryCount}";
        }

        public static bool operator ==(in BuffQualityCounts left, in BuffQualityCounts right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in BuffQualityCounts left, in BuffQualityCounts right)
        {
            return !left.Equals(right);
        }

        public static BuffQualityCounts operator +(in BuffQualityCounts left, in BuffQualityCounts right)
        {
            return new BuffQualityCounts(left.BaseCount + right.BaseCount, left.UncommonCount + right.UncommonCount, left.RareCount + right.RareCount, left.EpicCount + right.EpicCount, left.LegendaryCount + right.LegendaryCount);
        }

        public static BuffQualityCounts operator -(in BuffQualityCounts left, in BuffQualityCounts right)
        {
            return new BuffQualityCounts(left.BaseCount - right.BaseCount, left.UncommonCount - right.UncommonCount, left.RareCount - right.RareCount, left.EpicCount - right.EpicCount, left.LegendaryCount - right.LegendaryCount);
        }
    }
}
