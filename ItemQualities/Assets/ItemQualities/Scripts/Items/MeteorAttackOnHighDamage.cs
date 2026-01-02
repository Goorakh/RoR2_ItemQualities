using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class MeteorAttackOnHighDamage
    {
        const float BaseRadius = 10f;

        static float _radiusToPredictionScale = 1f / BaseRadius;
        static float _radiusToImpactScale = 1f / BaseRadius;

        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_DLC2_Items_MeteorAttackOnHighDamage.RunicMeteorStrikePredictionEffect_prefab).OnSuccess(meteorPredictionEffect =>
            {
                float baseScale = meteorPredictionEffect.transform.localScale.x;
                _radiusToPredictionScale *= baseScale;

                if (meteorPredictionEffect.TryGetComponent(out EffectComponent effectComponent))
                {
                    effectComponent.applyScale = true;
                }
            });

            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_DLC2_Items_MeteorAttackOnHighDamage.RunicMeteorStrikeImpact_prefab).OnSuccess(meteorImpactEffect =>
            {
                float baseRadius = meteorImpactEffect.transform.localScale.x;
                _radiusToImpactScale *= baseRadius;

                if (meteorImpactEffect.TryGetComponent(out EffectComponent effectComponent))
                {
                    effectComponent.applyScale = true;
                }
            });

            IL.RoR2.MeteorAttackOnHighDamageBodyBehavior.DetonateRunicLensMeteor += MeteorAttackOnHighDamageBodyBehavior_DetonateRunicLensMeteor;

            IL.RoR2.MeteorAttackOnHighDamageBodyBehavior.FixedUpdate += MeteorAttackOnHighDamageBodyBehavior_FixedUpdate;
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;
        }

        public static float GetMeteorRadius(CharacterBody attackerBody)
        {
            return getMeteorRadius(10f, attackerBody);
        }

        static float getMeteorRadius(float baseRadius, CharacterBody attackerBody)
        {
            float radius = baseRadius;
            
            if (attackerBody && attackerBody.inventory)
            {
                ItemQualityCounts meteorAttackOnHighDamage = attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.MeteorAttackOnHighDamage);

                if (meteorAttackOnHighDamage.TotalQualityCount > 0)
                {
                    float radiusIncrease = (0.75f * meteorAttackOnHighDamage.UncommonCount) +
                                           (1.00f * meteorAttackOnHighDamage.RareCount) +
                                           (1.50f * meteorAttackOnHighDamage.EpicCount) +
                                           (2.00f * meteorAttackOnHighDamage.LegendaryCount);

                    if (radiusIncrease > 0)
                    {
                        radius *= 1f + radiusIncrease;
                    }
                }

                radius = ExplodeOnDeath.GetExplosionRadius(radius, attackerBody);
            }

            return radius;
        }

        static void MeteorAttackOnHighDamageBodyBehavior_DetonateRunicLensMeteor(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchStfld<BlastAttack>(nameof(BlastAttack.radius))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[0].Next, MoveType.Before); // stfld BlastAttack.radius

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, MeteorAttackOnHighDamageBodyBehavior, float>>(getMeteorBlastRadius);

            static float getMeteorBlastRadius(float radius, MeteorAttackOnHighDamageBodyBehavior meteorItemBehavior)
            {
                return getMeteorRadius(radius, meteorItemBehavior ? meteorItemBehavior.body : null);
            }
        }

        static void MeteorAttackOnHighDamageBodyBehavior_FixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int meteorImpactEffectDataVariableIndex = -1;
            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdfld<MeteorAttackOnHighDamageBodyBehavior>(nameof(MeteorAttackOnHighDamageBodyBehavior.shouldSpawnMeteorStrikeVFX)),
                               x => x.MatchNewobj<EffectData>(),
                               x => x.MatchStloc(typeof(EffectData), il, out meteorImpactEffectDataVariableIndex),
                               x => x.MatchCallOrCallvirt(typeof(EffectManager), nameof(EffectManager.SpawnEffect))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[3].Next, MoveType.Before); // call EffectManager.SpawnEffect

            c.Emit(OpCodes.Ldloc, meteorImpactEffectDataVariableIndex);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<EffectData, MeteorAttackOnHighDamageBodyBehavior>>(handleQualityMeteorImpactEffectData);

            static void handleQualityMeteorImpactEffectData(EffectData impactEffectData, MeteorAttackOnHighDamageBodyBehavior meteorItemBehavior)
            {
                if (impactEffectData == null)
                    return;

                CharacterBody attackerBody = meteorItemBehavior ? meteorItemBehavior.body : null;
                impactEffectData.scale = GetMeteorRadius(attackerBody) * _radiusToImpactScale;
            }
        }

        static void GlobalEventManager_ProcessHitEnemy(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(DLC2Content.Items), nameof(DLC2Content.Items.MeteorAttackOnHighDamage)),
                               x => x.MatchNewobj<EffectData>(),
                               x => x.MatchStfld<EffectData>(nameof(EffectData.scale)),
                               x => x.MatchCallOrCallvirt(typeof(EffectManager), nameof(EffectManager.SpawnEffect))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.Before); // stfld EffectData.scale

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Func<float, DamageInfo, float>>(getQualityMeteorPredictionScale);

            static float getQualityMeteorPredictionScale(float scale, DamageInfo meteorItemBehavior)
            {
                CharacterBody attackerBody = meteorItemBehavior?.attacker ? meteorItemBehavior.attacker.GetComponent<CharacterBody>() : null;
                return GetMeteorRadius(attackerBody) * _radiusToPredictionScale * (scale / BaseRadius);
            }
        }
    }
}
