using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace EntityStates.QuestVolatileBattery
{
    public class QuestVolatileBatteryQualityMonitor : QuestVolatileBatteryBaseState
    {
        [NonSerialized]
        public static GameObject qualityBatteryPreDet;
        
        GameObject _vfxInstance;

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> VolatileBatteryPreDetLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_QuestVolatileBattery.VolatileBatteryPreDetonation_prefab);
            VolatileBatteryPreDetLoad.OnSuccess(VolatileBatteryPreDetPrefab =>
            {
                qualityBatteryPreDet = VolatileBatteryPreDetPrefab.InstantiateClone("QualityVolatileBatteryPreDet");
                GameObject.Destroy(qualityBatteryPreDet.GetComponent<LoopSound>());
                GameObject.Destroy(qualityBatteryPreDet.GetComponent<ShakeEmitter>());
                GameObject.Destroy(qualityBatteryPreDet.transform.Find("PP").gameObject);
                GameObject.Destroy(qualityBatteryPreDet.transform.Find("LightShafts").gameObject);
                GameObject.Destroy(qualityBatteryPreDet.transform.Find("Pulse").gameObject);
                GameObject.Destroy(qualityBatteryPreDet.transform.Find("Lightning").gameObject);
                GameObject.Destroy(qualityBatteryPreDet.transform.Find("Flames").gameObject);
                GameObject.Destroy(qualityBatteryPreDet.transform.Find("ShakeEmitter").gameObject);
                GameObject.Destroy(qualityBatteryPreDet.transform.Find("Mesh Pulse").gameObject);
                if (qualityBatteryPreDet.transform.Find("Sparks, Trail").TryGetComponent<ParticleSystem>(out ParticleSystem particleSystem))
                {
                    ParticleSystem.MainModule main = particleSystem.main;
                    main.simulationSpeed = 0.5f;
                    ParticleSystem.EmissionModule emission = particleSystem.emission;
                    emission.rateOverTimeMultiplier = 10;
                }
                args.ContentPack.prefabs.Add(qualityBatteryPreDet);
            });
            return VolatileBatteryPreDetLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            if (!NetworkServer.active)
                return;
            _vfxInstance = UnityEngine.Object.Instantiate(qualityBatteryPreDet, networkedBodyAttachment.attachedBody.transform);
            _vfxInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            _vfxInstance.transform.localScale = Vector3.one * networkedBodyAttachment.attachedBody.bestFitActualRadius;
        }

        public override void OnExit()
        {
            base.OnExit();
            if (_vfxInstance)
            {
                EntityState.Destroy(_vfxInstance);
            }
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
            if ((bool)base.attachedHealthComponent)
            {
                float combinedHealthFraction = base.attachedHealthComponent.combinedHealthFraction;
                if (combinedHealthFraction <= 0.5f)
                {
                    outer.SetNextState(new QuestVolatileBatteryQualityCountDown());
                }
            }
        }
    }
}
