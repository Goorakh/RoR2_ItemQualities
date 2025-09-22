namespace ItemQualities
{
    public struct ItemQualityCounts
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
    }
}
