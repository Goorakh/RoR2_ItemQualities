using RoR2;

namespace ItemQualities
{
    public struct QualityItemStackValues
    {
        public ItemQualityCounts PermanentStacks;

        public TempItemQualityCounts TemporaryStackValues;

        public ItemQualityCounts TotalStacks;

        public void AddItemCountsFrom(in Inventory.ItemStackValues itemStackValues, QualityTier qualityTier)
        {
            PermanentStacks[qualityTier] += itemStackValues.permanentStacks;
            TemporaryStackValues[qualityTier] += itemStackValues.temporaryStacksValue;
            TotalStacks[qualityTier] += itemStackValues.totalStacks;
        }

        public static QualityItemStackValues Create()
        {
            return new QualityItemStackValues();
        }
    }
}
