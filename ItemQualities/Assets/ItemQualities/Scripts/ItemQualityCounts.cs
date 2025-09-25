using System;

namespace ItemQualities
{
    public struct ItemQualityCounts : IEquatable<ItemQualityCounts>
    {
        public int BaseItemCount;
        public int UncommonCount;
        public int RareCount;
        public int EpicCount;
        public int LegendaryCount;

        public readonly int TotalCount => BaseItemCount + UncommonCount + RareCount + EpicCount + LegendaryCount;

        public ItemQualityCounts(int baseItemCount, int uncommonCount, int rareCount, int epicCount, int legendaryCount)
        {
            BaseItemCount = baseItemCount;
            UncommonCount = uncommonCount;
            RareCount = rareCount;
            EpicCount = epicCount;
            LegendaryCount = legendaryCount;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is ItemQualityCounts counts && Equals(counts);
        }

        public readonly bool Equals(ItemQualityCounts other)
        {
            return BaseItemCount == other.BaseItemCount &&
                   UncommonCount == other.UncommonCount &&
                   RareCount == other.RareCount &&
                   EpicCount == other.EpicCount &&
                   LegendaryCount == other.LegendaryCount;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(BaseItemCount, UncommonCount, RareCount, EpicCount, LegendaryCount);
        }

        public override readonly string ToString()
        {
            return $"Normal={BaseItemCount}, Uncommon={UncommonCount}, Rare={RareCount}, Epic={EpicCount}, Legendary={LegendaryCount}";
        }

        public static bool operator ==(ItemQualityCounts left, ItemQualityCounts right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ItemQualityCounts left, ItemQualityCounts right)
        {
            return !(left == right);
        }

        public static ItemQualityCounts operator +(ItemQualityCounts left, ItemQualityCounts right)
        {
            return new ItemQualityCounts(left.BaseItemCount + right.BaseItemCount, left.UncommonCount + right.UncommonCount, left.RareCount + right.RareCount, left.EpicCount + right.EpicCount, left.LegendaryCount + right.LegendaryCount);
        }

        public static ItemQualityCounts operator -(ItemQualityCounts left, ItemQualityCounts right)
        {
            return new ItemQualityCounts(left.BaseItemCount - right.BaseItemCount, left.UncommonCount - right.UncommonCount, left.RareCount - right.RareCount, left.EpicCount - right.EpicCount, left.LegendaryCount - right.LegendaryCount);
        }
    }
}
