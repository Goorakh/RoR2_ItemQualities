using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPaths.Version_1_35_0;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    static class CritAtLowerElevation
    {
        static GameObject _forceDownEffectPrefab;

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> critAtLowerElevationFullEffectLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC3_Items_CritAtLowerElevation.CritAtLowerElevationFullEffect_prefab);
            critAtLowerElevationFullEffectLoad.OnSuccess(critAtLowerElevationFullEffect =>
            {
                _forceDownEffectPrefab = critAtLowerElevationFullEffect.InstantiateClone("CritAtLowerElevationQualityForceDownEffect", false);

                Transform donutTransform = _forceDownEffectPrefab.transform.Find("Donut");
                if (donutTransform)
                {
                    donutTransform.localRotation *= Quaternion.Euler(180f, 0f, 0f);
                }
                else
                {
                    Log.Error("Failed to find donut transform");
                }

                foreach (ParticleSystem particleSystem in _forceDownEffectPrefab.GetComponentsInChildren<ParticleSystem>())
                {
                    ParticleSystem.MainModule main = particleSystem.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }

                args.ContentPack.effectDefs.Add(new EffectDef(_forceDownEffectPrefab));
            });

            return critAtLowerElevationFullEffectLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer]
        static void Init()
        {
            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        static void onServerDamageDealt(DamageReport damageReport)
        {
            if (damageReport?.damageInfo == null || damageReport.damageDealt <= 0 || damageReport.damageInfo.procCoefficient <= 0)
                return;

            if (!damageReport.victimBody)
                return;

            if ((damageReport.victimBody.bodyFlags & CharacterBody.BodyFlags.Unmovable) != 0 ||
                (damageReport.victimBody.bodyFlags & CharacterBody.BodyFlags.IgnoreKnockup) != 0)
            {
                return;
            }

            Inventory attackerInventory = damageReport.attackerBody ? damageReport.attackerBody.inventory : null;
            if (!attackerInventory)
                return;

            ItemQualityCounts critAtLowerElevation = attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.CritAtLowerElevation);
            if (critAtLowerElevation.TotalQualityCount == 0)
                return;

            float forceDownChance = (10f * critAtLowerElevation.UncommonCount) +
                                    (20f * critAtLowerElevation.RareCount) +
                                    (35f * critAtLowerElevation.EpicCount) +
                                    (50f * critAtLowerElevation.LegendaryCount);

            int forceDownCount = RollUtil.GetOverflowRoll(forceDownChance * damageReport.damageInfo.procCoefficient, damageReport.attackerMaster, damageReport.damageInfo.procChainMask.HasProc(ProcType.SureProc));
            if (forceDownCount <= 0)
                return;

            IPhysMotor victimMotor = damageReport.victim.GetComponent<IPhysMotor>();
            if (victimMotor is PseudoCharacterMotor)
                return;

            bool hasMotor = victimMotor != null && (victimMotor as Behaviour) != null;

            Rigidbody victimRigidbody = damageReport.victim.GetComponent<Rigidbody>();
            if ((!hasMotor || victimMotor is RigidbodyMotor) && victimRigidbody && victimRigidbody.isKinematic)
                return;

            if (hasMotor && victimMotor is CharacterMotor victimCharacterMotor && victimCharacterMotor.isGrounded)
                return;

            float pushDownForce;
            switch (critAtLowerElevation.HighestQuality)
            {
                case QualityTier.Uncommon:
                    pushDownForce = 30f;
                    break;
                case QualityTier.Rare:
                    pushDownForce = 35f;
                    break;
                case QualityTier.Epic:
                    pushDownForce = 40f;
                    break;
                case QualityTier.Legendary:
                    pushDownForce = 50f;
                    break;
                default:
                    Log.Error($"Quality tier {critAtLowerElevation.HighestQuality} is not implemented");
                    return;
            }

            pushDownForce *= forceDownCount;

            switch (damageReport.victimBody.hullClassification)
            {
                case HullClassification.Human:
                    pushDownForce *= 1f;
                    break;
                case HullClassification.Golem:
                    pushDownForce *= 0.75f;
                    break;
                case HullClassification.BeetleQueen:
                    pushDownForce *= 0.5f;
                    break;
            }

            if (damageReport.victimIsChampion || damageReport.victimIsBoss)
            {
                pushDownForce *= 0.5f;
            }

            PhysForceInfo forceInfo = new PhysForceInfo
            {
                force = new Vector3(0f, -pushDownForce, 0f),
                massIsOne = true
            };

            bool appliedForce = false;
            if (hasMotor)
            {
                victimMotor.ApplyForceImpulse(forceInfo);
                appliedForce = true;
            }
            else if (victimRigidbody && !victimRigidbody.isKinematic)
            {
                victimRigidbody.AddForceWithInfo(forceInfo);
                appliedForce = true;
            }

            if (appliedForce && _forceDownEffectPrefab)
            {
                EffectData effectData = new EffectData
                {
                    origin = damageReport.victimBody.corePosition,
                    scale = damageReport.victimBody.radius * 0.75f,
                };

                EffectManager.SpawnEffect(_forceDownEffectPrefab, effectData, true);
            }
        }
    }
}
