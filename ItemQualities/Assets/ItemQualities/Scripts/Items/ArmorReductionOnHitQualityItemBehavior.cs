using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    public sealed class ArmorReductionOnHitQualityItemBehavior : QualityItemBodyBehavior
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Authority | QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.ArmorReductionOnHit;

        private static GameObject tracerEffectPrefab;

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> laserTurbineTracerEffectLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_LaserTurbine.TracerLaserTurbine_prefab);
            laserTurbineTracerEffectLoad.OnSuccess(laserTurbineTracerEffectPrefab =>
            {
                tracerEffectPrefab = laserTurbineTracerEffectPrefab.InstantiateClone("ArmorReductionOnHitQualityTracer", false);

                if (tracerEffectPrefab.ExpectComponent(out Tracer tracer))
                {
                    tracer.speed = 100f;
                }

                if (tracerEffectPrefab.ExpectComponent(out DestroyOnTimer destroyOnTimer))
                {
                    destroyOnTimer.duration = 0.6f;
                }

                LineRenderer lineRenderer = tracerEffectPrefab.GetComponentInChildren<LineRenderer>();
                if (lineRenderer)
                {
                    lineRenderer.widthMultiplier = 4f;

                    if (lineRenderer.ExpectComponent(out AnimateShaderAlpha animateShaderAlpha))
                    {
                        animateShaderAlpha.timeMax = 0.6f;
                    }
                }
                else
                {
                    Log.Error($"Failed to find line renderer for tracer {Util.GetGameObjectHierarchyName(tracerEffectPrefab)}");
                }

                ShakeEmitter shakeEmitter = tracerEffectPrefab.GetComponentInChildren<ShakeEmitter>();
                if (shakeEmitter)
                {
                    shakeEmitter.enabled = false;
                }

                args.ContentPack.effectDefs.Add(new EffectDef(tracerEffectPrefab));
            });

            return laserTurbineTracerEffectLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        private static readonly int primaryAttacksPerLaser = 5;

        private int attackCounterAuthority = 0;

        private void OnEnable()
        {
            attackCounterAuthority = 0;
            Body.onSkillActivatedAuthority += onSkillActivatedAuthority;
        }

        private void OnDisable()
        {
            Body.onSkillActivatedAuthority -= onSkillActivatedAuthority;

            if (NetworkServer.active)
            {
                Body.RemoveAllQualityBuffs(ItemQualitiesContent.BuffQualityGroups.ArmorReductionOnHitCounter);
            }
        }

        private void onSkillActivatedAuthority(GenericSkill skill)
        {
            if (ReferenceEquals(skill, Body.skillLocator?.primary))
            {
                attackCounterAuthority++;
                if (attackCounterAuthority >= primaryAttacksPerLaser)
                {
                    attackCounterAuthority = 0;
                    firePrimaryBeamAuthority();
                }

                Body.SetBuffCountAuthority(ItemQualitiesContent.BuffQualityGroups.ArmorReductionOnHitCounter.GetBuffIndex(Stacks.HighestQuality), attackCounterAuthority);
            }
        }

        private void firePrimaryBeamAuthority()
        {
            ref readonly ItemQualityCounts armorReductionOnHit = ref Stacks;

            float damageCoefficient = (armorReductionOnHit.UncommonCount * 5f) +
                                      (armorReductionOnHit.RareCount * 9f) +
                                      (armorReductionOnHit.EpicCount * 15f) +
                                      (armorReductionOnHit.LegendaryCount * 20f);

            Ray aimRay = Body.inputBank.GetAimRay();

            new BulletAttack
            {
                origin = aimRay.origin,
                aimVector = aimRay.direction,
                owner = gameObject,
                weapon = gameObject,
                muzzleName = "Chest",
                bulletCount = 1,
                falloffModel = BulletAttack.FalloffModel.None,
                damage = damageCoefficient * Body.damage,
                procCoefficient = 1f,
                isCrit = Body.RollCrit(),
                damageType = DamageType.Generic,
                damageColorIndex = DamageColorIndex.Item,
                stopperMask = 0,
                hitMask = LayerIndex.CommonMasks.bullet,
                allowTrajectoryAimAssist = false,
                maxSpread = 0f,
                minSpread = 0f,
                queryTriggerInteraction = QueryTriggerInteraction.Ignore,
                trajectoryAimAssistMultiplier = 0f,
                tracerEffectPrefab = tracerEffectPrefab,
                radius = 1f,
                maxDistance = 30f,
            }.Fire();
        }

        protected override void OnStacksChanged()
        {
            base.OnStacksChanged();
            if (NetworkServer.active)
            {
                Body.ConvertQualityBuffsToTier(ItemQualitiesContent.BuffQualityGroups.ArmorReductionOnHitCounter, Stacks.HighestQuality);
            }
        }
    }
}
