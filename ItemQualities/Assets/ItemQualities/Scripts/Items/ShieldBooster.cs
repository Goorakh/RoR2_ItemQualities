using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using RoR2;
using RoR2BepInExPack.GameAssetPaths.Version_1_35_0;
using System;
using System.Reflection;
using UnityEngine;

namespace ItemQualities.Items
{
    static class ShieldBooster
    {
        public static event Action<CharacterBody> OnShieldBoosterBreakServerGlobal;

        static float _defaultShieldBreakBlastRadius = 12.5f;

        [SystemInitializer]
        static void Init()
        {
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_DLC3_Items_ShieldBooster.ShieldEMPPulseEffect_prefab).OnSuccess(fixShieldBreakEffectScaling);
            AddressableUtil.LoadAssetAsync<GameObject>(RoR2_DLC3_Items_ShieldBooster.ShieldEMPPulseEffectVoid_prefab).OnSuccess(fixShieldBreakEffectScaling);

            static void fixShieldBreakEffectScaling(GameObject shieldBreakEffectPrefab)
            {
                foreach (ParticleSystem particleSystem in shieldBreakEffectPrefab.GetComponentsInChildren<ParticleSystem>())
                {
                    ParticleSystem.MainModule main = particleSystem.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
            }

            MethodInfo takeDamageProcessMethod = typeof(HealthComponent).GetMethod(nameof(HealthComponent.TakeDamageProcess), (BindingFlags)(-1));
            if (takeDamageProcessMethod != null)
            {
                using DynamicMethodDefinition dmd = new DynamicMethodDefinition(takeDamageProcessMethod);
                using ILContext il = new ILContext(dmd.Definition);

                ILCursor c = new ILCursor(il);

                float shieldBreakBlastRadius = -1f;
                if (c.TryGotoNext(x => x.MatchLdsfld(typeof(DLC3Content.Items), nameof(DLC3Content.Items.ShieldBooster))) &&
                    c.TryGotoNext(x => x.MatchLdcR4(out shieldBreakBlastRadius),
                                  x => x.MatchStfld<BlastAttack>(nameof(BlastAttack.radius))))
                {
                    _defaultShieldBreakBlastRadius = shieldBreakBlastRadius;
                    Log.Debug($"Found default shield break blast radius: {_defaultShieldBreakBlastRadius}");
                }
                else
                {
                    Log.Error($"Failed to find default shield break blast radius, assuming hardcoded value of {_defaultShieldBreakBlastRadius}");
                }
            }
            else
            {
                Log.Error("Failed to find HealthComponent.TakeDamageProcess method");
            }

            On.RoR2.HealthComponent.GetShieldBoosterDamage += HealthComponent_GetShieldBoosterDamage;

            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        public static float GetShieldBreakBlastRadius(CharacterBody body)
        {
            return getShieldBreakBlastRadius(_defaultShieldBreakBlastRadius, body);
        }

        static float getShieldBreakBlastRadius(float baseRadius, CharacterBody body)
        {
            float radius = baseRadius;

            if (body && body.inventory)
            {
                ItemQualityCounts shieldBooster = body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ShieldBooster);
                if (shieldBooster.TotalQualityCount > 0)
                {
                    BuffQualityCounts shieldBoosterBuff = body.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.ShieldBoosterBuff);
                    float shieldBoosterBuffFraction = shieldBoosterBuff.TotalQualityCount / 100f;
                    if (shieldBoosterBuffFraction > 0)
                    {
                        float maxRadiusIncrease = (5f * shieldBooster.UncommonCount) + 
                                                  (12f * shieldBooster.RareCount) +
                                                  (18f * shieldBooster.EpicCount) +
                                                  (25f * shieldBooster.LegendaryCount);

                        float radiusIncrease = shieldBoosterBuffFraction * maxRadiusIncrease;

                        radius += radiusIncrease;
                    }
                }

                radius = ExplodeOnDeath.GetExplosionRadius(radius, body);
            }

            return radius;
        }

        static float HealthComponent_GetShieldBoosterDamage(On.RoR2.HealthComponent.orig_GetShieldBoosterDamage orig, HealthComponent self, int stack)
        {
            float damage = orig(self, stack);

            if (self.body && self.body.inventory)
            {
                ItemQualityCounts shieldBooster = self.body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.ShieldBooster);
                if (shieldBooster.TotalQualityCount > 0)
                {
                    BuffQualityCounts shieldBoosterBuff = self.body.GetBuffCounts(ItemQualitiesContent.BuffQualityGroups.ShieldBoosterBuff);
                    float shieldBoosterBuffFraction = shieldBoosterBuff.TotalQualityCount / 100f;
                    if (shieldBoosterBuffFraction > 0)
                    {
                        float maxDamageIncrease = (2f * shieldBooster.UncommonCount) +
                                                  (5f * shieldBooster.RareCount) +
                                                  (10f * shieldBooster.EpicCount) +
                                                  (15f * shieldBooster.LegendaryCount);

                        float damageIncrease = shieldBoosterBuffFraction * maxDamageIncrease;

                        damage += self.body.damage * damageIncrease;
                    }
                }
            }

            return damage;
        }

        static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(DLC3Content.Items), nameof(DLC3Content.Items.ShieldBooster)),
                               x => x.MatchStfld<BlastAttack>(nameof(BlastAttack.radius)),
                               x => x.MatchCallOrCallvirt<BlastAttack>(nameof(BlastAttack.Fire))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.Before); // stfld BlastAttack.radius

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, HealthComponent, float>>(getBlastRadius);

            static float getBlastRadius(float radius, HealthComponent healthComponent)
            {
                return getShieldBreakBlastRadius(radius, healthComponent ? healthComponent.body : null);
            }

            c.Goto(foundCursors[2].Next, MoveType.After); // call BlastAttack.Fire

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<HealthComponent>>(onShieldBoosterBreak);

            static void onShieldBoosterBreak(HealthComponent healthComponent)
            {
                CharacterBody body = healthComponent ? healthComponent.body : null;
                if (body)
                {
                    OnShieldBoosterBreakServerGlobal?.Invoke(body);
                }
            }

            c.Goto(foundCursors[1].Next, MoveType.Before); // stfld BlastAttack.radius
            if (c.TryGotoPrev(MoveType.Before,
                              x => x.MatchStfld<EffectData>(nameof(EffectData.scale))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, HealthComponent, float>>(getBlastEffectScale);

                static float getBlastEffectScale(float scale, HealthComponent healthComponent)
                {
                    float radius = getBlastRadius(_defaultShieldBreakBlastRadius, healthComponent);
                    return radius / _defaultShieldBreakBlastRadius;
                }
            }
            else
            {
                Log.Warning("Failed to find shield break");
            }
        }
    }
}
