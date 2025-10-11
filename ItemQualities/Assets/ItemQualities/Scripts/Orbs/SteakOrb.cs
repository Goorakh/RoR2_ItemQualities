using RoR2;
using RoR2.Orbs;
using UnityEngine;

namespace ItemQualities.Orbs
{
    public class SteakOrb : Orb
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

        public float SteakBonus;

        CharacterMasterExtraStatsTracker _targetMasterStats;

        public override void Begin()
        {
            duration = Mathf.Max(Time.fixedDeltaTime, distanceToTarget / 30f);

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

            HealthComponent targetHealthComponent = target ? target.healthComponent : null;
            CharacterBody targetBody = targetHealthComponent ? targetHealthComponent.body : null;
            CharacterMaster targetMaster = targetBody ? targetBody.master : null;
            if (targetMaster)
            {
                _targetMasterStats = targetMaster.GetComponent<CharacterMasterExtraStatsTracker>();
            }
        }

        public override void OnArrival()
        {
            if (_targetMasterStats)
            {
                _targetMasterStats.SteakBonus += SteakBonus;
            }
        }
    }
}
