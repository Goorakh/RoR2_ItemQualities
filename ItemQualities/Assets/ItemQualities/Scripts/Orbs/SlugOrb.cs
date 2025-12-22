using RoR2;
using RoR2.Orbs;
using UnityEngine;

namespace ItemQualities.Orbs
{
    public sealed class SlugOrb : Orb
    {
        static EffectIndex _orbEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        static void Init()
        {
            _orbEffectIndex = EffectCatalogUtils.FindEffectIndex("InfusionOrbEffect");
            if (_orbEffectIndex == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find orb effect index");
            }
        }

        public int SlugBuffCount;

        public override void Begin()
        {
            duration = Mathf.Max(Time.fixedDeltaTime, distanceToTarget / 50f);

            if (_orbEffectIndex != EffectIndex.Invalid)
            {
                EffectData effectData = new EffectData
                {
                    origin = origin,
                    genericFloat = duration
                };

                effectData.SetHurtBoxReference(target);

                EffectManager.SpawnEffect(_orbEffectIndex, effectData, true);
            }
        }

        public override void OnArrival()
        {
            if (target && target.healthComponent && target.healthComponent.body)
            {
                for (int i = 0; i < SlugBuffCount; i++)
                {
                    target.healthComponent.body.AddBuff(ItemQualitiesContent.Buffs.SlugHealth);
                }
            }
        }
    }
}
