using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.ContentManagement;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemQualities
{
    internal static class EffectScalingFixer
    {
        private static bool _contentLock = false;

        private static readonly Dictionary<GameObject, Dictionary<int, EffectDef>> _fixedScalingPrefabCaches = new();

        public static void AddToContentPack(ContentPack contentPack)
        {
            if (_fixedScalingPrefabCaches.Count > 0)
            {
                List<EffectDef> effectDefs = new List<EffectDef>();

                foreach (Dictionary<int, EffectDef> scaledPrefabsCache in _fixedScalingPrefabCaches.Values)
                {
                    effectDefs.AddRange(scaledPrefabsCache.Values);
                }

                contentPack.effectDefs.Add(effectDefs.ToArray());
                Log.Debug($"Added {effectDefs.Count} scaling effect(s)");
            }
        }

        public static void OnContentFinalized()
        {
            _contentLock = true;
            _fixedScalingPrefabCaches.Clear();
        }

        public static EffectDef GetOrCreateFixedScalingCopy(GameObject effectPrefab, float defaultRadius)
        {
            if (!effectPrefab || !effectPrefab.TryGetComponent(out EffectComponent effectComponent) || effectComponent.applyScale)
                return null;

            if (_contentLock)
            {
                Log.Error("Cannot create EffectDef after content load window");
                return null;
            }

            defaultRadius = MathF.Round(defaultRadius, 1);

            if (defaultRadius <= 0f)
            {
                Log.Warning($"Default radius must be a positive value! effectPrefab={effectPrefab}, defaultRadius={defaultRadius}");
                return null;
            }

            Dictionary<int, EffectDef> scaledPrefabsCache = _fixedScalingPrefabCaches.GetOrAddNew(effectPrefab);

            int dictionaryKey = (int)(defaultRadius * 10);
            if (scaledPrefabsCache.TryGetValue(dictionaryKey, out EffectDef cachedScaledEffectDef))
                return cachedScaledEffectDef;

            EffectDef effectDef = new EffectDef(CreateFixedScalingCopy(effectPrefab, defaultRadius));
            scaledPrefabsCache.Add(dictionaryKey, effectDef);
            return effectDef;
        }

        public static GameObject CreateFixedScalingCopy(GameObject prefab, float defaultRadius)
        {
            GameObject scaleFixExplosionEffectPrefab = prefab.InstantiateClone($"{prefab.name}_ScaleFix_x{defaultRadius:F1}", false);

            if (scaleFixExplosionEffectPrefab.TryGetComponent(out EffectComponent effectComponent))
            {
                effectComponent.applyScale = true;
            }

            if (scaleFixExplosionEffectPrefab.transform.childCount > 0)
            {
                GameObject scalerObj = new GameObject("Scaler");
                scalerObj.transform.SetParent(scaleFixExplosionEffectPrefab.transform);
                scalerObj.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                scalerObj.transform.localScale = scaleFixExplosionEffectPrefab.transform.localScale * (1f / defaultRadius);

                for (int i = scaleFixExplosionEffectPrefab.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = scaleFixExplosionEffectPrefab.transform.GetChild(i);
                    if (child != scalerObj.transform)
                    {
                        child.SetParent(scalerObj.transform, false);
                        child.SetAsFirstSibling();
                    }
                }

                foreach (ParticleSystem particleSystem in scaleFixExplosionEffectPrefab.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = particleSystem.main;
                    switch (main.scalingMode)
                    {
                        case ParticleSystemScalingMode.Local:
                            Vector3 scale = particleSystem.transform.localScale * (1f / defaultRadius);
                            particleSystem.transform.SetParent(scaleFixExplosionEffectPrefab.transform, true);
                            particleSystem.transform.localScale = scale;

                            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                            break;
                    }
                }
            }
            else
            {
                Log.Warning($"Scaled effect {prefab.name} has no children, set prefab scale will be lost");
            }

            // Restore the original scale of the prefab, in case it is used elsewhere without setting the scale
            scaleFixExplosionEffectPrefab.transform.localScale *= defaultRadius;

            return scaleFixExplosionEffectPrefab;
        }
    }
}
