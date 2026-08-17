using ItemQualities;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace EntityStates.RoboBallBuddy
{
    public sealed class ChargeQualityGigaBeam : BaseSkillState
    {
        private static GameObject _chargeEffectPrefab;

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> beamWindUpLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC3_SolusWing.SolusWingOverheatBeamWindUp_prefab);
            beamWindUpLoad.OnSuccess(beamWindUpPrefab =>
            {
                _chargeEffectPrefab = EffectScalingFixer.CreateFixedScalingCopy(beamWindUpPrefab, 1f, "QualityGigaBeamChargeUp");

                SetGlobalScale setGlobalScale = _chargeEffectPrefab.GetComponentInChildren<SetGlobalScale>();
                if (setGlobalScale)
                {
                    Destroy(setGlobalScale);
                }

                ScaleParticleSystemDuration scaleParticleSystemDuration = _chargeEffectPrefab.AddComponent<ScaleParticleSystemDuration>();
                scaleParticleSystemDuration.particleSystems = _chargeEffectPrefab.GetComponentsInChildren<ParticleSystem>(true);
                scaleParticleSystemDuration.initialDuration = 5f;

                Transform lightTransform = _chargeEffectPrefab.transform.Find("Charge Up/Point Light");
                if (lightTransform && lightTransform.ExpectComponent(out Light light))
                {
                    light.range *= 0.2f;
                    light.intensity *= 0.2f;
                }
            });

            return beamWindUpLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        public static string chargeEffectMuzzleName;

        public static string chargeSoundName;

        public static float baseDuration;

        private float _duration;

        private GameObject _chargeEffectInstance;
        private EffectManagerHelper _chargeEffectInstance_efh;

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Skill;
        }

        public override void OnEnter()
        {
            base.OnEnter();

            _duration = baseDuration / attackSpeedStat;

            Util.PlayAttackSpeedSound(chargeSoundName, gameObject, attackSpeedStat * (EntityStates.SolusWing.SolusWingOverheatBeamChargeUp.baseDuration / baseDuration));

            Transform muzzleTransform = FindModelChild(chargeEffectMuzzleName);

            if (muzzleTransform)
            {
                if (EffectManager.ShouldUsePooledEffect(_chargeEffectPrefab))
                {
                    _chargeEffectInstance_efh = EffectManager.GetAndActivatePooledEffect(_chargeEffectPrefab, muzzleTransform, true);
                    _chargeEffectInstance = _chargeEffectInstance_efh.gameObject;
                }
                else
                {
                    _chargeEffectInstance = GameObject.Instantiate(_chargeEffectPrefab, muzzleTransform);
                }

                _chargeEffectInstance.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);

                _chargeEffectInstance.GetComponent<ScaleParticleSystemDuration>().newDuration = _duration;

                foreach (AnimateShaderAlpha animateShaderAlpha in _chargeEffectInstance.GetComponentsInChildren<AnimateShaderAlpha>())
                {
                    animateShaderAlpha.timeMax = _duration;
                }
            }

            if (isAuthority)
            {
                activatorSkillSlot.SetBlockedCooldownSkillState(true);
            }
        }

        public override void OnExit()
        {
            base.OnExit();

            if (_chargeEffectInstance_efh && _chargeEffectInstance_efh.OwningPool != null)
            {
                _chargeEffectInstance_efh.OwningPool.ReturnObject(_chargeEffectInstance_efh);
            }
            else if (_chargeEffectInstance)
            {
                Destroy(_chargeEffectInstance);
            }

            _chargeEffectInstance = null;
            _chargeEffectInstance_efh = null;

            if (isAuthority)
            {
                activatorSkillSlot.SetBlockedCooldownSkillState(false);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (isAuthority)
            {
                if (fixedAge >= _duration)
                {
                    outer.SetNextState(new FireQualityGigaBeam { activatorSkillSlot = activatorSkillSlot });
                }
            }
        }
    }
}
