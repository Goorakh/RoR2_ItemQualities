using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Projectile;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class MoreMissile
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.MissileUtils.FireMissile_Vector3_CharacterBody_ProcChainMask_GameObject_float_bool_GameObject_DamageColorIndex_Vector3_float_bool += MissileUtils_FireMissile;
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;
        }

        static int rollAdditionalMissileCount(CharacterBody attackerBody)
        {
            if (!attackerBody)
                return 0;

            ItemQualityCounts moreMissile = ItemQualitiesContent.ItemQualityGroups.MoreMissile.GetItemCountsEffective(attackerBody.inventory);
            if (moreMissile.TotalQualityCount <= 0)
                return 0;

            float moreMissileChance = (10f * moreMissile.UncommonCount) +
                                      (20f * moreMissile.RareCount) +
                                      (30f * moreMissile.EpicCount) +
                                      (40f * moreMissile.LegendaryCount);

            return RollUtil.GetOverflowRoll(moreMissileChance, attackerBody.master);
        }

        static void MissileUtils_FireMissile(ILContext il)
        {
            if (!il.Method.TryFindParameter<CharacterBody>("attackerBody", out ParameterDefinition attackerBodyParameter))
            {
                Log.Warning("Failed to find attackerBody parameter");
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchCallOrCallvirt<ProjectileManager>(nameof(ProjectileManager.FireProjectile))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            if (attackerBodyParameter != null)
            {
                c.Emit(OpCodes.Ldarg, attackerBodyParameter);
            }
            else
            {
                c.Emit(OpCodes.Ldnull);
            }

            c.EmitDelegate<Func<FireProjectileInfo, CharacterBody, FireProjectileInfo>>(tryFireExtraMissiles);

            static FireProjectileInfo tryFireExtraMissiles(FireProjectileInfo missileProjectileInfo, CharacterBody attackerBody)
            {
                if (!attackerBody && missileProjectileInfo.owner)
                {
                    attackerBody = missileProjectileInfo.owner.GetComponent<CharacterBody>();
                }

                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                int additionalMissileCount = rollAdditionalMissileCount(attackerBody);
                if (additionalMissileCount > 0)
                {
                    Vector3 initialDirection = missileProjectileInfo.rotation * Vector3.forward;

                    // Intentionally using position as a fallback axis instead of forward to match vanilla behavior
                    Vector3 missileRotationAxis = attackerBody.inputBank ? attackerBody.inputBank.aimDirection : attackerBody.transform.position;

                    int middleMissileCount = additionalMissileCount + 1;
                    int totalMissileCount = middleMissileCount + 2;
                    for (int i = 0; i < middleMissileCount; i++)
                    {
                        float missileAngle = Util.Remap(i + 1, 0, totalMissileCount - 1, -45f, 45f);
                        missileProjectileInfo.rotation = Util.QuaternionSafeLookRotation(Quaternion.AngleAxis(missileAngle, missileRotationAxis) * initialDirection);

                        // Last missile is the one vanilla code will spawn, so just set the rotation and pass it on
                        if (i < additionalMissileCount)
                        {
                            ProjectileManager.instance.FireProjectile(missileProjectileInfo);
                        }
                    }
                }

                return missileProjectileInfo;
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
                               x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.MissileVoid)),
                               x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.MoreMissile)),
                               x => x.MatchLdcI4(3)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.After); // ldc.i4 3

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Func<int, DamageInfo, int>>(getMoreMissileCount);

            static int getMoreMissileCount(int missileCount, DamageInfo damageInfo)
            {
                if (damageInfo?.attacker && damageInfo.attacker.TryGetComponent(out CharacterBody attackerBody))
                {
                    missileCount += rollAdditionalMissileCount(attackerBody);
                }

                return missileCount;
            }
        }
    }
}
