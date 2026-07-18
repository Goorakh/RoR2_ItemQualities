using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    internal static class SiphonOnLowHealth
    {
        public static GameObject ExplosionVFX { get; private set; }

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> igniteExplosionVFXLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_IgniteOnKill.IgniteExplosionVFX_prefab);
            igniteExplosionVFXLoad.OnSuccess(igniteExplosionVFX =>
            {
                ExplosionVFX = igniteExplosionVFXLoad.Result.InstantiateClone("SiphonOnLowHealthExplosionVFX", false);

                Transform flames = ExplosionVFX.transform.Find("Flames");
                if (flames)
                {
                    GameObject.Destroy(flames.gameObject);
                }

                Transform flash = ExplosionVFX.transform.Find("Flash");
                if (flash)
                {
                    GameObject.Destroy(flash.gameObject);
                }

                if (ExplosionVFX.ExpectComponent(out ParticleSystem particleSystem))
                {
                    var main = particleSystem.main;
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color32(0x2D, 0x27, 0x19, 0xFF));
                }

                args.ContentPack.effectDefs.Add(new EffectDef(ExplosionVFX));
            });

            return igniteExplosionVFXLoad.AsProgressCoroutine(args.ProgressReceiver);
        }
    }

    public sealed class SiphonOnLowHealthQualityItemBehavior : QualityItemBodyBehavior, IOnDamageDealtServerReceiver
    {
        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Server)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.SiphonOnLowHealth;

        private float _accumulatedDamageCoefficient;

        public void OnDamageDealtServer(DamageReport damageReport)
        {
            if (damageReport.damageInfo.inflictor &&
                damageReport.damageInfo.inflictor.GetComponent<SiphonNearbyController>())
            {
                _accumulatedDamageCoefficient += damageReport.damageDealt / Body.damage;

                ref readonly ItemQualityCounts siphonOnLowHealth = ref Stacks;

                float requiredDamageCoefficient;
                float explosionRadius;
                switch (siphonOnLowHealth.HighestQuality)
                {
                    case QualityTier.Uncommon:
                        requiredDamageCoefficient = 8f;
                        explosionRadius = 25f;
                        break;
                    case QualityTier.Rare:
                        requiredDamageCoefficient = 5f;
                        explosionRadius = 35f;
                        break;
                    case QualityTier.Epic:
                        requiredDamageCoefficient = 3f;
                        explosionRadius = 50f;
                        break;
                    case QualityTier.Legendary:
                        requiredDamageCoefficient = 2f;
                        explosionRadius = 75f;
                        break;
                    default:
                        requiredDamageCoefficient = 0f;
                        explosionRadius = 0f;
                        Log.Warning($"Quality tier {siphonOnLowHealth.HighestQuality} is not implemented");
                        break;
                }

                explosionRadius = ExplodeOnDeath.GetExplosionRadius(explosionRadius, Body);

                if (requiredDamageCoefficient > 0f && _accumulatedDamageCoefficient > requiredDamageCoefficient)
                {
                    int thresholdCount = (int)(_accumulatedDamageCoefficient / requiredDamageCoefficient);
                    _accumulatedDamageCoefficient -= thresholdCount * requiredDamageCoefficient;

                    float explosionDamageCoefficient = (siphonOnLowHealth.UncommonCount * 8f) +
                                                       (siphonOnLowHealth.RareCount * 10f) +
                                                       (siphonOnLowHealth.EpicCount * 12f) +
                                                       (siphonOnLowHealth.LegendaryCount * 15f);

                    EffectData effectData = new EffectData
                    {
                        origin = Body.corePosition,
                        scale = explosionRadius,
                    };

                    EffectManager.SpawnEffect(SiphonOnLowHealth.ExplosionVFX, effectData, true);

                    DamageTypeCombo damageType = DamageType.ClayGoo;
                    damageType.AddModdedDamageType(DamageTypes.Lifesteal50);

                    new BlastAttack
                    {
                        position = Body.corePosition,
                        radius = explosionRadius,
                        attacker = Body.gameObject,
                        teamIndex = Body.teamComponent.teamIndex,
                        baseDamage = (thresholdCount * explosionDamageCoefficient) * Body.damage,
                        crit = Body.RollCrit(),
                        damageType = damageType,
                        damageColorIndex = DamageColorIndex.Item,
                        procCoefficient = 1f,
                        baseForce = 50f,
                        bonusForce = Vector3.zero,
                        falloffModel = BlastAttack.FalloffModel.HalfLinear,
                        losType = BlastAttack.LoSType.None,
                        attackerFiltering = AttackerFiltering.Default,
                    }.Fire();
                }
            }
        }
    }
}
