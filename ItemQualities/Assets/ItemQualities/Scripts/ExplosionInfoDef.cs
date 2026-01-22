namespace ItemQualities
{
    public sealed class ExplosionInfoDef
    {
        public string Name = string.Empty;

        public ExplosionInfoIndex Index { get; internal set; } = ExplosionInfoIndex.None;

        public delegate float GetDefaultRangeDelegate();
        public GetDefaultRangeDelegate DefaultRangeGetter { private get; set; }

        public float GetDefaultRange()
        {
            return DefaultRangeGetter();
        }
    }
}
