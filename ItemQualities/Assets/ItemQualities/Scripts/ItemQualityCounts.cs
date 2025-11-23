using System;
using System.Runtime.InteropServices;

namespace ItemQualities
{
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct ItemQualityCounts : IEquatable<ItemQualityCounts>
    {
        [FieldOffset(0)]
        fixed int _itemCounts[(int)QualityTier.Count + 1];

        [FieldOffset(0)]
        public int BaseItemCount;

        [FieldOffset(sizeof(int) * 1)]
        public int UncommonCount;
        
        [FieldOffset(sizeof(int) * 2)]
        public int RareCount;
        
        [FieldOffset(sizeof(int) * 3)]
        public int EpicCount;

        [FieldOffset(sizeof(int) * 4)]
        public int LegendaryCount;

        public readonly int TotalCount => BaseItemCount + UncommonCount + RareCount + EpicCount + LegendaryCount;

        public readonly int TotalQualityCount => UncommonCount + RareCount + EpicCount + LegendaryCount;

        public ref int this[QualityTier qualityTier] => ref _itemCounts[(int)qualityTier + 1];

        public readonly QualityTier HighestQuality
        {
            get
            {
                for (QualityTier qualityTier = QualityTier.Count - 1; qualityTier >= 0; qualityTier--)
                {
                    if (_itemCounts[(int)qualityTier + 1] > 0)
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

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct TempItemQualityCounts : IEquatable<TempItemQualityCounts>
    {
        [FieldOffset(0)]
        fixed float _itemCounts[(int)QualityTier.Count + 1];

        [FieldOffset(0)]
        public float BaseItemCount;
        
        [FieldOffset(sizeof(int) * 1)]
        public float UncommonCount;
        
        [FieldOffset(sizeof(int) * 2)]
        public float RareCount;
        
        [FieldOffset(sizeof(int) * 3)]
        public float EpicCount;

        [FieldOffset(sizeof(int) * 4)]
        public float LegendaryCount;

        public readonly float TotalCount => BaseItemCount + UncommonCount + RareCount + EpicCount + LegendaryCount;

        public readonly float TotalQualityCount => UncommonCount + RareCount + EpicCount + LegendaryCount;

        public ref float this[QualityTier qualityTier] => ref _itemCounts[(int)qualityTier + 1];

        public readonly QualityTier HighestQuality
        {
            get
            {
                for (QualityTier qualityTier = QualityTier.Count - 1; qualityTier >= 0; qualityTier--)
                {
                    if (_itemCounts[(int)qualityTier + 1] > 0)
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
