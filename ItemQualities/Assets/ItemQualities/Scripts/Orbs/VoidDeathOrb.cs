using R2API;
using RoR2;
using RoR2.Orbs;
using UnityEngine;

namespace ItemQualities.Orbs
{
    public class VoidDeathOrb : Orb
    {
        public static ModdedProcType VoidDeathOrbProcType { get; private set; } = ModdedProcType.Invalid;

        static EffectIndex _orbEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalog))]
        static void Init()
        {
            VoidDeathOrbProcType = ProcTypeAPI.ReserveProcType();

            _orbEffectIndex = EffectCatalog.FindEffectIndexFromPrefab(ItemQualitiesContent.Prefabs.VoidDeathOrbEffect);
            if (_orbEffectIndex == EffectIndex.Invalid)
            {
                Log.Error("Failed to find VoidDeathOrb effect index");
            }
        }

        public GameObject Attacker;

        public override void Begin()
        {
            duration = 0.4f;
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
            if (target)
            {
                HealthComponent victim = target.healthComponent;
                if (victim)
                {
                    ProcChainMask procChainMask = new ProcChainMask();
                    procChainMask.AddModdedProc(VoidDeathOrbProcType);

                    DamageInfo damageInfo = new DamageInfo
                    {
                        damage = 0f,
                        attacker = Attacker,
                        procChainMask = procChainMask,
                        procCoefficient = 0f,
                        position = target.transform.position,
                        damageColorIndex = DamageColorIndex.Void,
                        damageType = DamageType.VoidDeath | DamageType.BypassBlock | DamageType.Silent
                    };

                    victim.TakeDamage(damageInfo);
                    GlobalEventManager.instance.OnHitEnemy(damageInfo, victim.gameObject);
                    GlobalEventManager.instance.OnHitAll(damageInfo, victim.gameObject);
                }
            }
        }
    }
}
