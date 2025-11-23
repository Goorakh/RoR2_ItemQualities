namespace ItemQualities
{
    public struct QualityItemTransformResult
    {
        public QualityItemAndStackValues TakenItems;
        public QualityItemAndStackValues GivenItems;

        public static QualityItemTransformResult Create()
        {
            return new QualityItemTransformResult
            {
                TakenItems = QualityItemAndStackValues.Create(),
                GivenItems = QualityItemAndStackValues.Create(),
            };
        }
    }
}
