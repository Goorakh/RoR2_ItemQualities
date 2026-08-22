using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Assets.ItemQualities.Scripts
{
    [RequireComponent(typeof(EffectComponent))]
    public sealed class SetEffectDurationFromEffectData : MonoBehaviour
    {
        public ObjectScaleCurve[] objectScaleCurves = Array.Empty<ObjectScaleCurve>();

        public LightIntensityCurve[] lightIntensityCurves = Array.Empty<LightIntensityCurve>();

        public AnimateShaderAlpha[] animateShaderAlphas = Array.Empty<AnimateShaderAlpha>();

        public DestroyOnTimer[] destroyOnTimers = Array.Empty<DestroyOnTimer>();

        private EffectComponent _effectComponent;

        private void Awake()
        {
            _effectComponent = GetComponent<EffectComponent>();
            _effectComponent.OnEffectComponentReset += OnReset;
        }

        private void OnReset(bool hasEffectData)
        {
            if (!hasEffectData || _effectComponent.effectData == null)
            {
                return;
            }

            float duration = _effectComponent.effectData.genericFloat;

            foreach (ObjectScaleCurve objectScaleCurve in objectScaleCurves)
            {
                objectScaleCurve.timeMax = duration;
            }

            foreach (LightIntensityCurve lightIntensityCurve in lightIntensityCurves)
            {
                lightIntensityCurve.timeMax = duration;
            }

            foreach (AnimateShaderAlpha animateShaderAlpha in animateShaderAlphas)
            {
                animateShaderAlpha.timeMax = duration;
            }

            foreach (DestroyOnTimer destroyOnTimer in destroyOnTimers)
            {
                destroyOnTimer.duration = duration;
            }
        }
    }
}
