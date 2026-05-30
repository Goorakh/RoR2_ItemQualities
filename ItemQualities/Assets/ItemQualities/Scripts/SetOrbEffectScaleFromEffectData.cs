using RoR2;
using RoR2.Orbs;
using UnityEngine;

namespace ItemQualities
{
    [RequireComponent(typeof(EffectComponent))]
    [RequireComponent(typeof(OrbEffect))]
    public sealed class SetOrbEffectScaleFromEffectData : MonoBehaviour
    {
        public bool SetStartEffectScale;

        public bool SetEndEffectScale;

        private EffectComponent _effectComponent;
        private OrbEffect _orbEffect;

        private void Awake()
        {
            _effectComponent = GetComponent<EffectComponent>();
            _orbEffect = GetComponent<OrbEffect>();

            _effectComponent.OnEffectComponentReset += onReset;
        }

        private void onReset(bool hasEffectData)
        {
            if (SetStartEffectScale)
            {
                _orbEffect.startEffectScale = hasEffectData && _effectComponent.effectData != null ? _effectComponent.effectData.scale : 1f;
            }

            if (SetEndEffectScale)
            {
                _orbEffect.endEffectScale = hasEffectData && _effectComponent.effectData != null ? _effectComponent.effectData.scale : 1f;
            }
        }
    }
}
