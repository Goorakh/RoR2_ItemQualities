using R2API;
using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    public sealed class SiphonOnLowHealthQualityItemBehavior : QualityItemBodyBehavior, IOnDamageDealtServerReceiver
    {
        private static EffectIndex _explosionEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        private static void Init()
        {
            _explosionEffectIndex = EffectCatalogUtils.FindEffectIndex("ClayGrenadierMortarExplosion");
            if (_explosionEffectIndex == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find ClayGrenadierMortarExplosion effect index");
            }
        }

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

                    if (_explosionEffectIndex != EffectIndex.Invalid)
                    {
                        EffectData effectData = new EffectData
                        {
                            origin = Body.corePosition,
                            scale = explosionRadius,
                        };

                        EffectManager.SpawnEffect(_explosionEffectIndex, effectData, true);
                    }

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
