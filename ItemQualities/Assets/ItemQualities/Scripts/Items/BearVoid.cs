using HG;
using ItemQualities.ModCompatibility;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemQualities.Items
{
    internal static class BearVoid
    {
        private static readonly SphereSearch _fogSphereSearch = new SphereSearch
        {
            mask = LayerIndex.entityPrecise.mask,
            queryTriggerInteraction = QueryTriggerInteraction.Ignore,
        };

        [SystemInitializer]
        private static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += GetStatCoefficients;

            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        private static void GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            BuffQualityCounts bearVoidFog = sender.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.BearVoidFog);
            if (bearVoidFog.TotalQualityCount > 0)
            {
                args.moveSpeedReductionMultAdd += 0.2f;
            }
        }

        public static void TakeDamageModifier(ref float damageValue, HealthComponent victim, DamageInfo damageInfo)
        {
            if (!victim.body)
            {
                return;
            }

            CharacterBody attackerBody = damageInfo.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
            BodyIndex attackerBodyIndex = attackerBody ? attackerBody.bodyIndex : BodyIndex.None;
            SurvivorIndex attackerSurvivorIndex = SurvivorCatalog.GetSurvivorIndexFromBodyIndex(attackerBodyIndex);

            bool isVoidDamageType = damageInfo.damageType.HasModdedDamageType(DamageTypes.Void);
            bool attackerIsVoidBody = attackerBody && (attackerBody.bodyFlags & CharacterBody.BodyFlags.Void) != 0;

            // If DamageSourceForEnemies is not installed, let any damage count for non-survivor attackers since we (probably) have no tracking for skill damage
            bool attackIsSkillDamageEstimate = damageInfo.damageType.IsDamageSourceSkillBased ||
                                               (!DamageSourceForEnemies.Enabled && (attackerSurvivorIndex == SurvivorIndex.None));

            bool isVoidAttack = isVoidDamageType || (attackerIsVoidBody && attackIsSkillDamageEstimate);
            if (isVoidAttack)
            {
                BuffQualityCounts bearVoidFog = victim.body.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.BearVoidFog);

                float damageMultiplier = 1f + (bearVoidFog.UncommonCount * 0.03f) +
                                              (bearVoidFog.RareCount * 0.05f) +
                                              (bearVoidFog.EpicCount * 0.08f) +
                                              (bearVoidFog.LegendaryCount * 0.10f);

                if (damageMultiplier > 1)
                {
                    damageValue *= damageMultiplier;
                    damageInfo.damageColorIndex = DamageColorIndex.Void;
                }
            }
        }

        private static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdarg(0),
                               x => x.MatchLdfld<HealthComponent>(nameof(HealthComponent.body)),
                               x => x.MatchLdsfld(typeof(DLC1Content.Buffs), nameof(DLC1Content.Buffs.BearVoidReady)),
                               x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.RemoveBuff))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Action<HealthComponent, DamageInfo>>(onVoidBearBlock);

            static void onVoidBearBlock(HealthComponent victim, DamageInfo damageInfo)
            {
                if (!victim.body || !victim.body.inventory)
                {
                    return;
                }

                ItemQualityCounts bearVoid = victim.body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BearVoid);
                if (bearVoid.TotalQualityCount == 0)
                {
                    return;
                }

                int fogCount = bearVoid.TotalQualityCount;

                QualityTier qualityTier = bearVoid.HighestQuality;
                BuffIndex fogBuffIndex = ItemQualitiesContent.BuffQualityGroups.BearVoidFog.GetBuffIndex(qualityTier);

                float radius;
                switch (qualityTier)
                {
                    case QualityTier.Uncommon:
                        radius = 15f;
                        break;
                    case QualityTier.Rare:
                        radius = 25f;
                        break;
                    case QualityTier.Epic:
                        radius = 40f;
                        break;
                    case QualityTier.Legendary:
                        radius = 60f;
                        break;
                    default:
                        radius = 1f;
                        Log.Warning($"Quality tier {qualityTier} is not implemented");
                        break;
                }

                radius = ExplodeOnDeath.GetExplosionRadius(radius, victim.body);

                EffectData effectData = new EffectData
                {
                    origin = victim.body.corePosition,
                    scale = radius
                };

                EffectManager.SpawnEffect(ItemQualitiesContent.Prefabs.QualityBearVoidFogExplosion, effectData, true);

                TeamMask teamMask = TeamMask.allButNeutral;
                teamMask.RemoveTeam(victim.body.teamComponent.teamIndex);

                using var _ = ListPool<HurtBox>.RentCollection(out List<HurtBox> hurtBoxes);

                _fogSphereSearch.origin = victim.body.corePosition;
                _fogSphereSearch.radius = radius;
                _fogSphereSearch.RefreshCandidates()
                                .FilterCandidatesByHurtBoxTeam(teamMask)
                                .FilterCandidatesByDistinctHurtBoxEntities()
                                .GetHurtBoxes(hurtBoxes);

                foreach (HurtBox hurtBox in hurtBoxes)
                {
                    for (int i = 0; i < fogCount; i++)
                    {
                        hurtBox.healthComponent.body.AddBuff(fogBuffIndex);
                    }
                }
            }
        }
    }
}
