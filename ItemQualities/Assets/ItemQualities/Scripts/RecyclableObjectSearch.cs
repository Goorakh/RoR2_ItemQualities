using RoR2.DirectionalSearch;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ItemQualities
{
    public sealed class RecyclableObjectSearch : BaseDirectionalSearch<RecyclableObject, RecyclableObjectSearchSelector, RecyclableObjectSearchFilter>
    {
        public static readonly RecyclableObjectSearch SharedInstance = new RecyclableObjectSearch();

        public ref bool RequireRecyclable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref candidateFilter.RequireRecyclable;
        }

        public RecyclableObjectSearch(RecyclableObjectSearchSelector selector, RecyclableObjectSearchFilter candidateFilter)
            : base(selector, candidateFilter)
        {
        }

        public RecyclableObjectSearch()
            : base(default, default)
        {
        }
    }

    public readonly struct RecyclableObjectSearchSelector : IGenericWorldSearchSelector<RecyclableObject>
    {
        public readonly GameObject GetRootObject(RecyclableObject source)
        {
            return source.InteractableObject;
        }

        public readonly Transform GetTransform(RecyclableObject source)
        {
            return source.IndicatorTransform;
        }
    }

    public struct RecyclableObjectSearchFilter : IGenericDirectionalSearchFilter<RecyclableObject>
    {
        public bool RequireRecyclable;

        public readonly bool PassesFilter(RecyclableObject candidateInfo)
        {
            return !RequireRecyclable || candidateInfo.IsRecyclable;
        }
    }
}
