using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace EntityStates.QuestVolatileBattery
{
    public class QuestVolatileBatteryQualityCountDown : QuestVolatileBatteryBaseState
    {
        public static float duration;
        public static float explosionRadius;

        GameObject _vfxInstance;

        [NonSerialized]
        public static GameObject vfxPrefab;

        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_QuestVolatileBattery.VolatileBatteryPreDetonation_prefab).OnSuccess(VolatileBatteryPreDetonation =>
            {
                vfxPrefab = VolatileBatteryPreDetonation;
            });
        }

        public override void OnEnter()
        {
            base.OnEnter();
            if (!vfxPrefab || !networkedBodyAttachment.attachedBody)
            {
                return;
            }

            GameObject gameObject = UnityEngine.Object.Instantiate(vfxPrefab, networkedBodyAttachment.attachedBody.transform);
            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = Vector3.one * networkedBodyAttachment.attachedBody.bestFitActualRadius;
            _vfxInstance = gameObject;
        }

        public override void OnExit()
        {
            if (_vfxInstance)
            {
                EntityState.Destroy(_vfxInstance);
            }
            base.OnExit();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (NetworkServer.active)
            {
                FixedUpdateServer();
            }
        }

        private void FixedUpdateServer()
        {
            if (base.fixedAge >= duration)
            {
                ItemQualities.Equipments.QuestVolatileBattery.Detonate(base.gameObject, false);
            }
        }
    }
}
