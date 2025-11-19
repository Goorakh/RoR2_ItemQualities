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
            return obj is ItemQualityCounts other && Equals(other);
        }

        readonly bool IEquatable<ItemQualityCounts>.Equals(ItemQualityCounts other)
        {
            return Equals(other);
        }

        public readonly bool Equals(in ItemQualityCounts other)
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

        public static bool operator ==(in ItemQualityCounts left, in ItemQualityCounts right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in ItemQualityCounts left, in ItemQualityCounts right)
        {
            return !left.Equals(right);
        }

        public static ItemQualityCounts operator +(in ItemQualityCounts left, in ItemQualityCounts right)
        {
            return new ItemQualityCounts(left.BaseItemCount + right.BaseItemCount, left.UncommonCount + right.UncommonCount, left.RareCount + right.RareCount, left.EpicCount + right.EpicCount, left.LegendaryCount + right.LegendaryCount);
        }

        public static ItemQualityCounts operator -(in ItemQualityCounts left, in ItemQualityCounts right)
        {
            return new ItemQualityCounts(left.BaseItemCount - right.BaseItemCount, left.UncommonCount - right.UncommonCount, left.RareCount - right.RareCount, left.EpicCount - right.EpicCount, left.LegendaryCount - right.LegendaryCount);
        }

        public static explicit operator TempItemQualityCounts(in ItemQualityCounts itemCounts)
        {
            return new TempItemQualityCounts(itemCounts.BaseItemCount, itemCounts.UncommonCount, itemCounts.RareCount, itemCounts.EpicCount, itemCounts.LegendaryCount);
        }
    }

    public struct TempItemQualityCounts : IEquatable<TempItemQualityCounts>
    {
        public float BaseItemCount;
        public float UncommonCount;
        public float RareCount;
        public float EpicCount;
        public float LegendaryCount;

        public readonly float TotalCount => BaseItemCount + UncommonCount + RareCount + EpicCount + LegendaryCount;

        public readonly float TotalQualityCount => UncommonCount + RareCount + EpicCount + LegendaryCount;

        public float this[QualityTier qualityTier]
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

        public TempItemQualityCounts(float baseItemCount, float uncommonCount, float rareCount, float epicCount, float legendaryCount)
        {
            BaseItemCount = Math.Max(0, baseItemCount);
            UncommonCount = Math.Max(0, uncommonCount);
            RareCount = Math.Max(0, rareCount);
            EpicCount = Math.Max(0, epicCount);
            LegendaryCount = Math.Max(0, legendaryCount);
        }

        public override readonly bool Equals(object obj)
        {
            return obj is TempItemQualityCounts other && Equals(other);
        }

        readonly bool IEquatable<TempItemQualityCounts>.Equals(TempItemQualityCounts other)
        {
            return Equals(other);
        }

        public readonly bool Equals(in TempItemQualityCounts other)
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

        public static bool operator ==(in TempItemQualityCounts left, in TempItemQualityCounts right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in TempItemQualityCounts left, in TempItemQualityCounts right)
        {
            return !left.Equals(right);
        }

        public static TempItemQualityCounts operator +(in TempItemQualityCounts left, in TempItemQualityCounts right)
        {
            return new TempItemQualityCounts(left.BaseItemCount + right.BaseItemCount, left.UncommonCount + right.UncommonCount, left.RareCount + right.RareCount, left.EpicCount + right.EpicCount, left.LegendaryCount + right.LegendaryCount);
        }

        public static TempItemQualityCounts operator -(in TempItemQualityCounts left, in TempItemQualityCounts right)
        {
            return new TempItemQualityCounts(left.BaseItemCount - right.BaseItemCount, left.UncommonCount - right.UncommonCount, left.RareCount - right.RareCount, left.EpicCount - right.EpicCount, left.LegendaryCount - right.LegendaryCount);
        }

        public static explicit operator ItemQualityCounts(in TempItemQualityCounts tempCounts)
        {
            return new ItemQualityCounts((int)tempCounts.BaseItemCount, (int)tempCounts.UncommonCount, (int)tempCounts.RareCount, (int)tempCounts.EpicCount, (int)tempCounts.LegendaryCount);
        }
    }
}
