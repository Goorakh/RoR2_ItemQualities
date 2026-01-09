using AK.Wwise;
using HG;
using HG.Coroutines;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPaths.Version_1_35_0;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    static class DronesDropDynamite
    {
        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> droneBallDotZoneLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC3_Drone_Tech.DroneBallDotZone_prefab);
            AsyncOperationHandle<GameObject> droneTechBodyLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC3_Drone_Tech.DroneTechBody_prefab);

            ParallelProgressCoroutine loadCoroutine = new ParallelProgressCoroutine(args.ProgressReceiver);
            loadCoroutine.Add(droneBallDotZoneLoad);
            loadCoroutine.Add(droneTechBodyLoad);

            yield return loadCoroutine;

            if (!droneBallDotZoneLoad.AssertLoaded())
                yield break;

            GameObject droneShootableAttachmentPrefab = droneBallDotZoneLoad.Result.InstantiateClone(nameof(ItemQualitiesContent.NetworkedPrefabs.DroneShootableAttachment));

            ProjectileController projectileController = droneShootableAttachmentPrefab.GetComponent<ProjectileController>();
            DroneBallShootableController droneBallShootableController = droneShootableAttachmentPrefab.GetComponent<DroneBallShootableController>();
            ProjectileImpactExplosion projectileImpactExplosion = droneShootableAttachmentPrefab.GetComponent<ProjectileImpactExplosion>();
            HurtBoxGroup hurtBoxGroup = droneShootableAttachmentPrefab.GetComponentInChildren<HurtBoxGroup>();

            GameObject damageEffectPrefab = droneBallShootableController.damageEffectPrefab;
            Renderer[] modelRenderers = droneBallShootableController.renderers.Where(r => r).ToArray();
            Gradient damageColorGradient = droneBallShootableController.damageColorGradient;
            Transform rangeIndicatorTransform = droneBallShootableController.visualizerTransform;

            Transform fxTransform = droneBallShootableController.transform.Find("FX");

            foreach (Renderer modelRenderer in modelRenderers)
            {
                if (modelRenderer.transform.parent == fxTransform && modelRenderer.transform != rangeIndicatorTransform)
                {
                    modelRenderer.transform.SetParent(rangeIndicatorTransform, true);
                }
            }

            for (int i = fxTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = fxTransform.GetChild(i);
                if (child.GetComponent<HitBox>() || child.GetComponent<HurtBox>())
                {
                    child.SetParent(droneBallShootableController.transform, true);
                }
            }

            GameObject explosionEffect = projectileImpactExplosion.explosionEffect;

            IgnoredCollisionsProvider ignoredCollisionsProvider = droneShootableAttachmentPrefab.AddComponent<IgnoredCollisionsProvider>();
            ignoredCollisionsProvider.Colliders = Array.ConvertAll(hurtBoxGroup.hurtBoxes, h => h.GetComponent<Collider>());

            NetworkedBodyAttachment networkedBodyAttachment = droneShootableAttachmentPrefab.AddComponent<NetworkedBodyAttachment>();
            networkedBodyAttachment.forceHostAuthority = true;
            networkedBodyAttachment.shouldParentToAttachedBody = true;

            DroneShootableAttachmentController droneShootableController = droneShootableAttachmentPrefab.AddComponent<DroneShootableAttachmentController>();
            droneShootableController.HitEffectPrefab = damageEffectPrefab;
            droneShootableController.IndicatorRenderers = modelRenderers;
            droneShootableController.DamageColorGradient = damageColorGradient;
            droneShootableController.RangeIndicator = rangeIndicatorTransform;
            droneShootableController.ExplosionEffect = explosionEffect;
            droneShootableController.HurtBoxGroup = hurtBoxGroup;
            droneShootableController.FxRoot = fxTransform.gameObject;
            droneShootableController.IgnoredCollisionsProvider = ignoredCollisionsProvider;

            if (projectileController.flightSoundLoop)
            {
                fxTransform.gameObject.AddComponent<LoopSoundPlayer>().loopDef = projectileController.flightSoundLoop;
            }

            if (droneShootableAttachmentPrefab.TryGetComponent(out TeamComponent teamComponent))
            {
                teamComponent._teamIndex = TeamIndex.Neutral;
            }

            ProcDamageModifier procDamageModifier = droneShootableAttachmentPrefab.EnsureComponent<ProcDamageModifier>();
            procDamageModifier.ProcCoefficientMultiplier = 0f;
            procDamageModifier.DamageTypeToAdd = DamageType.Silent;

            GameObject.Destroy(droneShootableAttachmentPrefab.GetComponent<BuffWard>());
            GameObject.Destroy(droneBallShootableController);
            GameObject.Destroy(projectileImpactExplosion);
            GameObject.Destroy(droneShootableAttachmentPrefab.GetComponent<ProjectileFuse>());
            GameObject.Destroy(droneShootableAttachmentPrefab.GetComponent<ProjectileDamage>());
            GameObject.Destroy(projectileController);
            GameObject.Destroy(droneShootableAttachmentPrefab.GetComponent<ProjectileNetworkTransform>());
            GameObject.Destroy(droneShootableAttachmentPrefab.GetComponent<Rigidbody>());
            GameObject.Destroy(droneShootableAttachmentPrefab.GetComponent<AssignTeamFilterToTeamComponent>());
            GameObject.Destroy(droneShootableAttachmentPrefab.GetComponent<TeamFilter>());
            droneShootableAttachmentPrefab.layer = LayerIndex.defaultLayer.intVal;

            Bank operatorBank = null;
            if (droneTechBodyLoad.Status == AsyncOperationStatus.Succeeded && droneTechBodyLoad.Result)
            {
                if (droneTechBodyLoad.Result.TryGetComponent(out AkBank operatorBodyBank))
                {
                    operatorBank = operatorBodyBank.data;
                }
            }

            if (operatorBank != null)
            {
                AkBank shieldBank = droneShootableAttachmentPrefab.AddComponent<AkBank>();
                shieldBank.data = operatorBank;
                shieldBank.triggerList = new List<int> { AkTriggerHandler.ON_ENABLE_TRIGGER_ID };
                shieldBank.unloadTriggerList = new List<int> { AkTriggerHandler.ON_DISABLE_TRIGGER_ID };
            }
            else
            {
                Log.Warning("Failed to load operator sound bank");
            }

            args.ContentPack.networkedObjectPrefabs.Add(droneShootableAttachmentPrefab);
        }
    }
}
