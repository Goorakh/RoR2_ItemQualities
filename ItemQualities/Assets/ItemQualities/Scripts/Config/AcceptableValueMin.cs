using BepInEx.Configuration;
using System;

namespace ItemQualities.Config
{
    public sealed class AcceptableValueMin<T> : AcceptableValueBase where T : IComparable
    {
        public T MinValue { get; }

        readonly string _cachedMinValueString;

        public AcceptableValueMin(T minValue, string customValueFormat = null) : base(typeof(T))
        {
            if (minValue == null)
            {
                throw new ArgumentNullException(nameof(minValue));
            }

            MinValue = minValue;

            _cachedMinValueString = string.IsNullOrEmpty(customValueFormat) ? MinValue.ToString() : string.Format("{0:" + customValueFormat + "}", MinValue);
        }

        public override object Clamp(object value)
        {
            if (MinValue.CompareTo(value) > 0)
            {
                return MinValue;
            }

            return value;
        }

        public override bool IsValid(object value)
        {
            return MinValue.CompareTo(value) <= 0;
        }

        public override string ToDescriptionString()
        {
            return $"# Acceptable value range: Greater than or equal to {_cachedMinValueString}";
        }
    }
}
