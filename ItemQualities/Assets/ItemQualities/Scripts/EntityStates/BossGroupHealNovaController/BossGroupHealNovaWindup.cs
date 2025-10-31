using ItemQualities;
using RoR2;

namespace EntityStates.BossGroupHealNovaController
{
    public class BossGroupHealNovaWindup : EntityState
    {
        static EffectIndex _chargeEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        static void Init()
        {
            _chargeEffectIndex = EffectCatalogUtils.FindEffectIndex("ChargeTPHealingNova");
            if (_chargeEffectIndex == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find charge effect index");
            }
        }

        public static float Duration;

        public override void OnEnter()
        {
            base.OnEnter();

            if (_chargeEffectIndex != EffectIndex.Invalid)
            {
                EffectManager.SpawnEffect(_chargeEffectIndex, new EffectData
                {
                    origin = transform.position
                }, false);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (isAuthority && fixedAge >= Duration)
            {
                outer.SetNextStateToMain();
            }
        }
    }
}
