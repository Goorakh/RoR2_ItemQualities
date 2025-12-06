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

            ItemQualityCounts critAtLowerElevation = ItemQualitiesContent.ItemQualityGroups.CritAtLowerElevation.GetItemCountsEffective(attackerInventory);
            if (critAtLowerElevation.TotalQualityCount == 0)
                return;

            float forceDownChance = (10f * critAtLowerElevation.UncommonCount) +
                                    (20f * critAtLowerElevation.RareCount) +
                                    (35f * critAtLowerElevation.EpicCount) +
                                    (50f * critAtLowerElevation.LegendaryCount);

            int forceDownCount = RollUtil.GetOverflowRoll(forceDownChance * damageReport.damageInfo.procCoefficient, damageReport.attackerMaster);
            if (forceDownCount <= 0)
                return;

            Rigidbody victimRigidbody = damageReport.victim.GetComponent<Rigidbody>();
            if (victimRigidbody && victimRigidbody.isKinematic)
                return;

            IPhysMotor victimMotor = damageReport.victim.GetComponent<IPhysMotor>();

            CharacterMotor victimCharacterMotor = victimMotor as CharacterMotor;
            if (victimCharacterMotor && victimCharacterMotor.isGrounded)
                return;

            float pushDownForce;
            switch (critAtLowerElevation.HighestQuality)
            {
                case QualityTier.Uncommon:
                    pushDownForce = 10f;
                    break;
                case QualityTier.Rare:
                    pushDownForce = 12f;
                    break;
                case QualityTier.Epic:
                    pushDownForce = 15f;
                    break;
                case QualityTier.Legendary:
                    pushDownForce = 20f;
                    break;
                default:
                    Log.Error($"Quality tier {critAtLowerElevation.HighestQuality} is not implemented");
                    return;
            }

            pushDownForce *= forceDownCount;

            if (damageReport.victimIsChampion || damageReport.victimIsBoss)
            {
                pushDownForce *= 0.4f;
            }

            PhysForceInfo forceInfo = new PhysForceInfo
            {
                force = new Vector3(0f, -pushDownForce, 0f),
                massIsOne = true
            };

            bool appliedForce = false;
            if (victimMotor != null)
            {
                victimMotor.ApplyForceImpulse(forceInfo);
                appliedForce = true;
            }
            else if (victimRigidbody)
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
