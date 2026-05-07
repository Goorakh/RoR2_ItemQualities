using EntityStates.QuestVolatileBattery;
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

namespace EntityStates.QuestVolatileBatteryQuality
{
    public sealed class QuestVolatileBatteryQualityMonitor : QuestVolatileBatteryBaseState
    {
        [NonSerialized]
        public static GameObject qualityBatteryPreDetonationEffect;
        
        GameObject _vfxInstance;

        [ContentInitializer]
        static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> volatileBatteryPreDetonationLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_QuestVolatileBattery.VolatileBatteryPreDetonation_prefab);
            volatileBatteryPreDetonationLoad.OnSuccess(volatileBatteryPreDetonationPrefab =>
            {
                qualityBatteryPreDetonationEffect = volatileBatteryPreDetonationPrefab.InstantiateClone("QualityVolatileBatteryPreDetonation", false);
                
                if (qualityBatteryPreDetonationEffect.TryGetComponent<ShakeEmitter>(out ShakeEmitter shakeEmitter))
                {
                    Destroy(shakeEmitter);
                }

                if (qualityBatteryPreDetonationEffect.TryGetComponent<LoopSound>(out LoopSound loopSound))
                {
                    Destroy(loopSound);
                }

                Transform postProcess = qualityBatteryPreDetonationEffect.transform.Find("PP");
                if (postProcess)
                {
                    Destroy(postProcess.gameObject);
                }

                Transform lightShafts = qualityBatteryPreDetonationEffect.transform.Find("LightShafts");
                if (lightShafts)
                {
                    Destroy(lightShafts.gameObject);
                }

                Transform pulse = qualityBatteryPreDetonationEffect.transform.Find("Pulse");
                if (pulse)
                {
                    Destroy(pulse.gameObject);
                }

                Transform sparks = qualityBatteryPreDetonationEffect.transform.Find("Sparks, Trail");
                if (sparks && sparks.TryGetComponent(out ParticleSystem particleSystem))
                {
                    ParticleSystem.MainModule main = particleSystem.main;
                    main.simulationSpeed = 0.5f;
                    ParticleSystem.EmissionModule emission = particleSystem.emission;
                    emission.rateOverTimeMultiplier = 10;
                }

                args.ContentPack.prefabs.Add(qualityBatteryPreDetonationEffect);
            });

            return volatileBatteryPreDetonationLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        public override void OnEnter()
        {
            base.OnEnter();

            _vfxInstance = GameObject.Instantiate(qualityBatteryPreDetonationEffect, networkedBodyAttachment.attachedBody.transform);
            _vfxInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            _vfxInstance.transform.localScale = Vector3.one * networkedBodyAttachment.attachedBody.bestFitActualRadius;
        }

        public override void OnExit()
        {
            base.OnExit();
            if (_vfxInstance)
            {
                Destroy(_vfxInstance);
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
            if (attachedHealthComponent && attachedHealthComponent.combinedHealthFraction <= 0.5f)
            {
                outer.SetNextState(new QuestVolatileBatteryQualityCountDown());
            }
        }
    }
}
