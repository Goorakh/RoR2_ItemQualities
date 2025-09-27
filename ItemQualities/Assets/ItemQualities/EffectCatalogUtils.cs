using RoR2;
using System.Collections.Generic;

namespace ItemQualities
{
    public static class EffectCatalogUtils
    {
        static readonly Dictionary<string, EffectIndex> _effectIndexByPrefabName = new Dictionary<string, EffectIndex>();

        [SystemInitializer(typeof(EffectCatalog))]
        static void Init()
        {
            _effectIndexByPrefabName.Clear();
            _effectIndexByPrefabName.EnsureCapacity(EffectCatalog.effectCount);

            for (EffectIndex effectIndex = 0; (int)effectIndex < EffectCatalog.effectCount; effectIndex++)
            {
                EffectDef effectDef = EffectCatalog.GetEffectDef(effectIndex);
                if (effectDef != null && !string.IsNullOrWhiteSpace(effectDef.prefabName))
                {
                    if (_effectIndexByPrefabName.ContainsKey(effectDef.prefabName))
                    {
                        Log.Warning($"Duplicate effect prefab name '{effectDef.prefabName}'");
                    }

                    _effectIndexByPrefabName[effectDef.prefabName] = effectIndex;
                }
            }
        }

        public static EffectIndex FindEffectIndex(string effectPrefabName)
        {
            return _effectIndexByPrefabName.GetValueOrDefault(effectPrefabName, EffectIndex.Invalid);
        }
    }
}
