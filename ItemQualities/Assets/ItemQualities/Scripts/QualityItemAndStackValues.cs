using RoR2;

namespace ItemQualities
{
    public struct QualityItemAndStackValues
    {
        public ItemIndex ItemIndex;

        public QualityItemStackValues StackValues;

        public static QualityItemAndStackValues Create()
        {
            return new QualityItemAndStackValues
            {
                ItemIndex = ItemIndex.None,
                StackValues = QualityItemStackValues.Create(),
            };
        }
    }
}
