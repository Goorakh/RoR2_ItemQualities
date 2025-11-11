using HG.Coroutines;
using ItemQualities.Utilities;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    static class FireRing
    {
        [SystemInitializer]
        static IEnumerator Init()
        {
            AsyncOperationHandle<GameObject> fireTornadoLoad = AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_ElementalRings.FireTornado_prefab);
            AsyncOperationHandle<GameObject> fireTornadoGhostLoad = AddressableUtil.LoadAssetAsync<GameObject>(RoR2_Base_ElementalRings.FireTornadoGhost_prefab);

            ParallelCoroutine loadCoroutine = new ParallelCoroutine();
            loadCoroutine.Add(fireTornadoLoad);
            loadCoroutine.Add(fireTornadoGhostLoad);

            yield return loadCoroutine;

            if (fireTornadoLoad.Status != AsyncOperationStatus.Succeeded || !fireTornadoLoad.Result)
            {
                Log.Error($"Failed to load FireTornado prefab: {fireTornadoLoad.OperationException}");
                yield break;
            }

            if (fireTornadoGhostLoad.Status != AsyncOperationStatus.Succeeded || !fireTornadoGhostLoad.Result)
            {
                Log.Error($"Failed to load FireTornadoGhost prefab: {fireTornadoGhostLoad.OperationException}");
                yield break;
            }

            GameObject fireTornado = fireTornadoLoad.Result;
            fireTornado.AddComponent<FireTornadoProjectileController>();
            fireTornado.AddComponent<ScaleProjectileGhostDurationsToLifetime>();

            float fireTornadoDuration = 3.3f;
            if (fireTornado.TryGetComponent(out ProjectileSimple projectileSimple))
            {
                fireTornadoDuration = projectileSimple.lifetime;
            }

            GameObject fireTornadoGhost = fireTornadoGhostLoad.Result;

            ProjectileGhostController ghostController = fireTornadoGhost.GetComponent<ProjectileGhostController>();
            ghostController.inheritScaleFromProjectile = true;
            
            ParticleSystem[] fireTornadoParticleSystems = fireTornadoGhost.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particleSystem in fireTornadoParticleSystems)
            {
                ParticleSystem.MainModule main = particleSystem.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }

            ScaleParticleSystemDuration scaleParticleSystemDuration = fireTornadoGhost.AddComponent<ScaleParticleSystemDuration>();
            scaleParticleSystemDuration.initialDuration = fireTornadoDuration;
            scaleParticleSystemDuration.particleSystems = fireTornadoParticleSystems;
        }
    }
}
