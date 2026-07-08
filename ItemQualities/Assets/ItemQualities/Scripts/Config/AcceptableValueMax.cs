using BepInEx.Configuration;
using System;

namespace ItemQualities.Config
{
    public sealed class AcceptableValueMax<T> : AcceptableValueBase where T : IComparable
    {
        public T MaxValue { get; }

        private readonly string _cachedMaxValueString;

        public AcceptableValueMax(T maxValue, string customValueFormat = null) : base(typeof(T))
        {
            if (maxValue == null)
            {
                throw new ArgumentNullException(nameof(maxValue));
            }

            MaxValue = maxValue;

            _cachedMaxValueString = string.IsNullOrEmpty(customValueFormat) ? MaxValue.ToString() : string.Format("{0:" + customValueFormat + "}", MaxValue);
        }

        public override object Clamp(object value)
        {
            if (MaxValue.CompareTo(value) < 0)
            {
                return MaxValue;
            }

            return value;
        }

        public override bool IsValid(object value)
        {
            return MaxValue.CompareTo(value) >= 0;
        }

        public override string ToDescriptionString()
        {
            return $"# Acceptable value range: Lesser than or equal to {_cachedMaxValueString}";
        }
    }
}
