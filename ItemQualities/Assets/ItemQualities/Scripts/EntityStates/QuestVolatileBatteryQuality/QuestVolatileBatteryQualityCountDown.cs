using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;
using UnityEngine.Networking;

namespace EntityStates.QuestVolatileBatteryQuality
{
    public sealed class QuestVolatileBatteryQualityCountDown : QuestVolatileBatteryQualityBaseState
    {
        private static GameObject _countdownEffectPrefab;

        public static float duration;
        public static float explosionRadius;

        private GameObject _countdownEffectInstance;

        [SystemInitializer]
        private static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_QuestVolatileBattery.VolatileBatteryPreDetonation_prefab).OnSuccess(volatileBatteryPreDetonation =>
            {
                _countdownEffectPrefab = volatileBatteryPreDetonation;
            });
        }

        public override void OnEnter()
        {
            base.OnEnter();

            if (!_countdownEffectPrefab)
                return;

            _countdownEffectInstance = GameObject.Instantiate(_countdownEffectPrefab, transform);
            _countdownEffectInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            if (attachedBody)
            {
                _countdownEffectInstance.transform.localScale = Vector3.one * attachedBody.bestFitActualRadius;
            }
        }

        public override void OnExit()
        {
            if (_countdownEffectInstance)
            {
                Destroy(_countdownEffectInstance);
            }

            base.OnExit();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (attachedBody)
            {
                transform.position = attachedBody.corePosition;
            }

            if (NetworkServer.active)
            {
                FixedUpdateServer();
            }
        }

        private void FixedUpdateServer()
        {
            if (fixedAge >= duration)
            {
                ItemQualities.Equipments.QuestVolatileBattery.Detonate(gameObject);
                Destroy(gameObject);
            }
        }
    }
}
