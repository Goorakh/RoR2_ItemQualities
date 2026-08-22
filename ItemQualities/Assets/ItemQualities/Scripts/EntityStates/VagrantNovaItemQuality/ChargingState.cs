using ItemQualities.Items;
using RoR2;
using UnityEngine;

namespace EntityStates.VagrantNovaItemQuality
{
    public sealed class ChargingState : BaseVagrantNovaItemQualityState
    {
        public static float baseDuration = 1.5f;

        public static string chargeSound;

        private float duration;

        private GameObject chargeVfxInstance;

        private GameObject areaIndicatorVfxInstance;

        public override void OnEnter()
        {
            base.OnEnter();

            float attachedBodyAttackSpeed = attachedBody ? attachedBody.attackSpeed : 1f;

            duration = baseDuration / attachedBodyAttackSpeed;

            if (attachedBody)
            {
                transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);

                chargeVfxInstance = GameObject.Instantiate(VagrantMonster.ChargeMegaNova.chargingEffectPrefab, position, rotation);
                chargeVfxInstance.transform.localScale = Vector3.one * 0.25f;

                Util.PlayAttackSpeedSound(chargeSound, gameObject, (baseDuration / VagrantNovaItem.ChargeState.baseDuration) * attachedBodyAttackSpeed);

                areaIndicatorVfxInstance = GameObject.Instantiate(VagrantMonster.ChargeMegaNova.areaIndicatorPrefab, position, rotation);

                ObjectScaleCurve chargeEffectScaleCurve = areaIndicatorVfxInstance.GetComponent<ObjectScaleCurve>();
                chargeEffectScaleCurve.timeMax = duration;
                chargeEffectScaleCurve.baseScale = Vector3.one * (ExplodeOnDeath.GetExplosionRadius(DetonateState.blastRadius, attachedBody) * 2f);

                areaIndicatorVfxInstance.GetComponent<AnimateShaderAlpha>().timeMax = duration;
            }

            RoR2Application.onLateUpdate += OnLateUpdate;
        }

        public override void OnExit()
        {
            RoR2Application.onLateUpdate -= OnLateUpdate;

            if (chargeVfxInstance)
            {
                Destroy(chargeVfxInstance);
                chargeVfxInstance = null;
            }

            if (areaIndicatorVfxInstance)
            {
                Destroy(areaIndicatorVfxInstance);
                areaIndicatorVfxInstance = null;
            }

            base.OnExit();
        }

        private void OnLateUpdate()
        {
            if (chargeVfxInstance)
            {
                chargeVfxInstance.transform.position = transform.position;
            }

            if (areaIndicatorVfxInstance)
            {
                areaIndicatorVfxInstance.transform.position = transform.position;
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (isAuthority && fixedAge >= duration)
            {
                outer.SetNextState(new DetonateState());
            }
        }
    }
}
