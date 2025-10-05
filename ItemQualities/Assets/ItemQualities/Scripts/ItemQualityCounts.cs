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

        public readonly int TotalQualityCount => UncommonCount + RareCount + EpicCount + LegendaryCount;

        public int this[QualityTier qualityTier]
        {
            readonly get
            {
                switch (qualityTier)
                {
                    case QualityTier.None:
                        return BaseItemCount;
                    case QualityTier.Uncommon:
                        return UncommonCount;
                    case QualityTier.Rare:
                        return RareCount;
                    case QualityTier.Epic:
                        return EpicCount;
                    case QualityTier.Legendary:
                        return LegendaryCount;
                    default:
                        throw new NotImplementedException($"Quality tier {qualityTier} is not implemented");
                }
            }
            set
            {
                switch (qualityTier)
                {
                    case QualityTier.None:
                        BaseItemCount = value;
                        break;
                    case QualityTier.Uncommon:
                        UncommonCount = value;
                        break;
                    case QualityTier.Rare:
                        RareCount = value;
                        break;
                    case QualityTier.Epic:
                        EpicCount = value;
                        break;
                    case QualityTier.Legendary:
                        LegendaryCount = value;
                        break;
                    default:
                        throw new NotImplementedException($"Quality tier {qualityTier} is not implemented");
                }
            }
        }

        public ItemQualityCounts(int baseItemCount, int uncommonCount, int rareCount, int epicCount, int legendaryCount)
        {
            BaseItemCount = Math.Max(0, baseItemCount);
            UncommonCount = Math.Max(0, uncommonCount);
            RareCount = Math.Max(0, rareCount);
            EpicCount = Math.Max(0, epicCount);
            LegendaryCount = Math.Max(0, legendaryCount);
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
