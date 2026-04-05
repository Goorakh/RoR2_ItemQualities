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
                qualityBatteryPreDet = VolatileBatteryPreDetPrefab.InstantiateClone("QualityVolatileBatteryPreDet", false);
                
                if (qualityBatteryPreDet.TryGetComponent<ShakeEmitter>(out ShakeEmitter shakeEmitter))
                {
                    GameObject.Destroy(shakeEmitter);
                }
                if (qualityBatteryPreDet.TryGetComponent<LoopSound>(out LoopSound loopSound))
                {
                    GameObject.Destroy(loopSound);
                }
                Transform PP = qualityBatteryPreDet.transform.Find("PP");
                if (PP)
                {
                    GameObject.Destroy(PP.gameObject);
                }
                Transform LightShafts = qualityBatteryPreDet.transform.Find("LightShafts");
                if (LightShafts)
                {
                    GameObject.Destroy(LightShafts.gameObject);
                }
                Transform Pulse = qualityBatteryPreDet.transform.Find("Pulse");
                if (Pulse)
                {
                    GameObject.Destroy(Pulse.gameObject);
                }
                Transform Lightning = qualityBatteryPreDet.transform.Find("Lightning");
                if (Lightning)
                {
                    GameObject.Destroy(Pulse.gameObject);
                }
                Transform Flames = qualityBatteryPreDet.transform.Find("Flames");
                if (Flames)
                {
                    GameObject.Destroy(Pulse.gameObject);
                }
                Transform ShakeEmitter = qualityBatteryPreDet.transform.Find("ShakeEmitter");
                if (ShakeEmitter)
                {
                    GameObject.Destroy(Pulse.gameObject);
                }
                Transform Mesh_Pulse = qualityBatteryPreDet.transform.Find("Mesh Pulse");
                if (Mesh_Pulse)
                {
                    GameObject.Destroy(Pulse.gameObject);
                }
                Transform Sparks_Trail = qualityBatteryPreDet.transform.Find("Sparks, Trail");
                if (Sparks_Trail && Sparks_Trail.TryGetComponent<ParticleSystem>(out ParticleSystem particleSystem))
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
