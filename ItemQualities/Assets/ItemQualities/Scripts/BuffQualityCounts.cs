using System;

namespace ItemQualities
{
    public struct BuffQualityCounts : IEquatable<BuffQualityCounts>
    {
        public int BaseCount;
        public int UncommonCount;
        public int RareCount;
        public int EpicCount;
        public int LegendaryCount;

        public readonly int TotalCount => BaseCount + UncommonCount + RareCount + EpicCount + LegendaryCount;

        public readonly int TotalQualityCount => UncommonCount + RareCount + EpicCount + LegendaryCount;

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

        public readonly bool Equals(BuffQualityCounts other)
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

        public static bool operator ==(BuffQualityCounts left, BuffQualityCounts right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BuffQualityCounts left, BuffQualityCounts right)
        {
            return !(left == right);
        }

        public static BuffQualityCounts operator +(BuffQualityCounts left, BuffQualityCounts right)
        {
            return new BuffQualityCounts(left.BaseCount + right.BaseCount, left.UncommonCount + right.UncommonCount, left.RareCount + right.RareCount, left.EpicCount + right.EpicCount, left.LegendaryCount + right.LegendaryCount);
        }

        public static BuffQualityCounts operator -(BuffQualityCounts left, BuffQualityCounts right)
        {
            return new BuffQualityCounts(left.BaseCount - right.BaseCount, left.UncommonCount - right.UncommonCount, left.RareCount - right.RareCount, left.EpicCount - right.EpicCount, left.LegendaryCount - right.LegendaryCount);
        }
    }
}
