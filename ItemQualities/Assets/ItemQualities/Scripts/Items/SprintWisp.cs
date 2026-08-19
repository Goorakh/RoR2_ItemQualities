using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Items;
using RoR2.Orbs;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    internal static class SprintWisp
    {
        public static GameObject GreaterWispOrbEffectPrefab { get; private set; }

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> wispOrbEffectLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_SprintWisp.WispOrbEffect_prefab);
            wispOrbEffectLoad.OnSuccess(wispOrbEffectPrefab =>
            {
                GreaterWispOrbEffectPrefab = wispOrbEffectPrefab.InstantiateClone("QualityGreaterWispOrbEffect", false);

                if (GreaterWispOrbEffectPrefab.TryGetComponent(out EffectComponent effectComponent))
                {
                    effectComponent.applyScale = true;
                }

                if (GreaterWispOrbEffectPrefab.TryGetComponent(out OrbEffect orbEffect))
                {
                    // We want to manually create the effect at the end in order to scale it with explosion size
                    orbEffect.endEffect = null;
                }

                Transform flamesTransform = GreaterWispOrbEffectPrefab.transform.Find("Flames");
                if (flamesTransform && flamesTransform.TryGetComponent(out ParticleSystemRenderer flamesRenderer))
                {
                    Material fireMaterial = args.ContentPack.materials.Find("mat" + nameof(ItemQualitiesContent.Materials.SprintWispQualityFire));
                    if (fireMaterial)
                    {
                        flamesRenderer.sharedMaterial = fireMaterial;
                    }
                    else
                    {
                        Log.Error("Failed to find flame material SprintWispQualityFire, was it included in the content pack?");
                    }
                }
                else
                {
                    Log.Error($"Failed to find Flames renderer on {GreaterWispOrbEffectPrefab}");
                }

                args.ContentPack.effectDefs.Add(new EffectDef(GreaterWispOrbEffectPrefab));
            });

            return wispOrbEffectLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.Items.SprintWispBodyBehavior.Fire += SprintWispBodyBehavior_Fire;
        }

        private static void SprintWispBodyBehavior_Fire(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchNewobj<DevilOrb>()))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<DevilOrb, SprintWispBodyBehavior, DevilOrb>>(getOrb);

            static DevilOrb getOrb(DevilOrb orb, SprintWispBodyBehavior sprintWispBodyBehavior)
            {
                if (sprintWispBodyBehavior &&
                    sprintWispBodyBehavior.body &&
                    sprintWispBodyBehavior.body.inventory)
                {
                    ItemQualityCounts sprintWisp = sprintWispBodyBehavior.body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SprintWisp);
                    if (sprintWisp.TotalQualityCount > 0)
                    {
                        float greaterWispChance;
                        switch (sprintWisp.HighestQuality)
                        {
                            case QualityTier.Uncommon:
                                greaterWispChance = 10f;
                                break;
                            case QualityTier.Rare:
                                greaterWispChance = 30f;
                                break;
                            case QualityTier.Epic:
                                greaterWispChance = 50f;
                                break;
                            case QualityTier.Legendary:
                                greaterWispChance = 75f;
                                break;
                            default:
                                greaterWispChance = 0;
                                Log.Warning($"Quality tier {sprintWisp.HighestQuality} is not implemented!");
                                break;
                        }

                        if (RollUtil.CheckRoll(greaterWispChance, sprintWispBodyBehavior.body.master, false))
                        {
                            orb = new GreaterWispOrb();
                        }
                    }
                }

                return orb;
            }

            VariableDefinition devilOrbVar = null;
            if (c.TryGotoNext(MoveType.After,
                              x => x.MatchStloc<DevilOrb>(il, out devilOrbVar)))
            {
                c.Emit(OpCodes.Ldloc, devilOrbVar);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Action<DevilOrb, SprintWispBodyBehavior>>(setupDevilOrb);

                static void setupDevilOrb(DevilOrb devilOrb, SprintWispBodyBehavior sprintWispBodyBehavior)
                {
                    if (devilOrb is not GreaterWispOrb greaterWispOrb)
                        return;

                    if (!sprintWispBodyBehavior || !sprintWispBodyBehavior.body || !sprintWispBodyBehavior.body.inventory)
                        return;

                    ItemQualityCounts sprintWisp = sprintWispBodyBehavior.body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.SprintWisp);
                    if (sprintWisp.TotalQualityCount > 0)
                    {
                        float explosionRadius = (7f * sprintWisp.UncommonCount) +
                                                (15f * sprintWisp.RareCount) +
                                                (25f * sprintWisp.EpicCount) +
                                                (40f * sprintWisp.LegendaryCount);

                        greaterWispOrb.blastRadius = explosionRadius;

                        greaterWispOrb.scale = Mathf.Sqrt(greaterWispOrb.blastRadius / 2f);

                        float bonusDamageCoefficient = (4f * sprintWisp.UncommonCount) +
                                                       (6f * sprintWisp.RareCount) +
                                                       (8f * sprintWisp.EpicCount) +
                                                       (10f * sprintWisp.LegendaryCount);

                        greaterWispOrb.damageValue += sprintWispBodyBehavior.body.damage * bonusDamageCoefficient;
                    }
                }
            }
            else
            {
                Log.Error("Failed to find devilOrb variable");
            }
        }
    }

    public sealed class GreaterWispOrb : DevilOrb
    {
        private static EffectIndex _explosionEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        private static void Init()
        {
            _explosionEffectIndex = EffectCatalogUtils.FindEffectIndex("OmniExplosionVFXArchWisp");
            if (_explosionEffectIndex == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find explosion effect index");
            }
        }

        public float blastRadius = 5f;

        public float force = 50f;

        public override void Begin()
        {
            duration = distanceToTarget / 20f;

            EffectData effectData = new EffectData
            {
                scale = scale,
                origin = origin,
                genericFloat = duration
            };

            effectData.SetHurtBoxReference(target);

            EffectManager.SpawnEffect(SprintWisp.GreaterWispOrbEffectPrefab, effectData, true);
        }

        public override void OnArrival()
        {
            if (target)
            {
                float radius = blastRadius;
                if (attacker && attacker.TryGetComponent(out CharacterBody attackerBody))
                {
                    radius = ExplodeOnDeath.GetExplosionRadius(radius, attackerBody);
                }

                new BlastAttack
                {
                    position = target.transform.position,
                    radius = radius,
                    attacker = attacker,
                    baseDamage = damageValue,
                    crit = isCrit,
                    baseForce = force,
                    damageColorIndex = damageColorIndex,
                    inflictedHurtbox = target,
                    procCoefficient = procCoefficient,
                    procChainMask = procChainMask,
                    teamIndex = teamIndex
                }.Fire();

                if (_explosionEffectIndex != EffectIndex.Invalid)
                {
                    EffectManager.SpawnEffect(_explosionEffectIndex, new EffectData
                    {
                        origin = target.transform.position,
                        scale = radius,
                    }, true);
                }
            }
        }
    }
}
