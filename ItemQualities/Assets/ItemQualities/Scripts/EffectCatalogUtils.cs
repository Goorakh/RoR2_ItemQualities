using RoR2;
using System;
using System.Runtime.CompilerServices;

namespace ItemQualities
{
    [Obsolete("Use EffectCatalog instead")]
    public static class EffectCatalogUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use EffectCatalog.FindEffectIndex instead")]
        public static EffectIndex FindEffectIndex(string effectPrefabName)
        {
            return EffectCatalog.FindEffectIndex(effectPrefabName);
        }
    }
}
