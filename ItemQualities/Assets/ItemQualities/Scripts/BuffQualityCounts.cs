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

        public int this[QualityTier qualityTier]
        {
            readonly get
            {
                switch (qualityTier)
                {
                    case QualityTier.None:
                        return BaseCount;
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
                        BaseCount = value;
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

        public readonly QualityTier HighestQuality
        {
            get
            {
                for (QualityTier qualityTier = QualityTier.Count - 1; qualityTier >= 0; qualityTier--)
                {
                    if (this[qualityTier] > 0)
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
