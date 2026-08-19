using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Items
{
    internal static class MoreMissile
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.MissileUtils.FireMissile_Vector3_CharacterBody_ProcChainMask_GameObject_float_bool_GameObject_DamageColorIndex_Vector3_float_bool += MissileUtils_FireMissile;
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;
        }

        public static int RollAdditionalMissileCount(CharacterBody attackerBody, bool sureProc)
        {
            if (!attackerBody || !attackerBody.inventory)
                return 0;

            ItemQualityCounts moreMissile = attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.MoreMissile);
            if (moreMissile.TotalQualityCount <= 0)
                return 0;

            float moreMissileChance = (50f * moreMissile.UncommonCount) +
                                      (100f * moreMissile.RareCount) +
                                      (150f * moreMissile.EpicCount) +
                                      (250f * moreMissile.LegendaryCount);

            return RollUtil.GetOverflowRoll(moreMissileChance, attackerBody.master, sureProc);
        }

        private static void MissileUtils_FireMissile(ILContext il)
        {
            if (!il.Method.TryFindParameter<CharacterBody>("attackerBody", out ParameterDefinition attackerBodyParameter))
            {
                Log.Error("Failed to find attackerBody parameter");
            }

            if (!il.Method.TryFindParameter<ProcChainMask>(out ParameterDefinition procChainMaskParameter))
            {
                Log.Error("Failed to find ProcChainMaskParameter");
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

            if (procChainMaskParameter != null)
            {
                c.Emit(OpCodes.Ldarg, procChainMaskParameter);
            }
            else
            {
                c.EmitDefaultValue<ProcChainMask>();
            }

            c.EmitDelegate<Func<FireProjectileInfo, CharacterBody, ProcChainMask, FireProjectileInfo>>(tryFireExtraMissiles);

            static FireProjectileInfo tryFireExtraMissiles(FireProjectileInfo missileProjectileInfo, CharacterBody attackerBody, ProcChainMask procChainMask)
            {
                if (!attackerBody && missileProjectileInfo.owner)
                {
                    attackerBody = missileProjectileInfo.owner.GetComponent<CharacterBody>();
                }

                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                int additionalMissileCount = RollAdditionalMissileCount(attackerBody, procChainMask.HasProc(ProcType.SureProc));
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

        private static void GlobalEventManager_ProcessHitEnemy(ILContext il)
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
                    missileCount += RollAdditionalMissileCount(attackerBody, damageInfo.procChainMask.HasProc(ProcType.SureProc));
                }

                return missileCount;
            }
        }
    }

    public sealed class MoreMissileQualityItemBehavior : QualityItemBodyBehavior
    {
        private static GameObject _missileProjectilePrefab;

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> missileProjectileLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Drones.MicroMissileProjectile_prefab);
            missileProjectileLoad.OnSuccess(missileProjectilePrefab =>
            {
                _missileProjectilePrefab = missileProjectilePrefab.InstantiateClone("QualityMoreMissileProjectile");

                if (_missileProjectilePrefab.ExpectComponent(out ProjectileController projectileController))
                {
                    projectileController.procCoefficient = 0f;
                }

                if (_missileProjectilePrefab.ExpectComponent(out MissileController missileController))
                {
                    missileController.maxVelocity = 25f;
                }

                args.ContentPack.projectilePrefabs.Add(_missileProjectilePrefab);
            });

            return missileProjectileLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [ItemGroupAssociation(QualityItemBehaviorUsageFlags.Authority)]
        private static ItemQualityGroup GetItemGroup() => ItemQualitiesContent.ItemQualityGroups.MoreMissile;

        private void OnEnable()
        {
            Body.onSkillActivatedAuthority += onSkillActivatedAuthority;
        }

        private void OnDisable()
        {
            Body.onSkillActivatedAuthority -= onSkillActivatedAuthority;
        }

        private void onSkillActivatedAuthority(GenericSkill skill)
        {
            if (skill.baseRechargeInterval >= 5f)
            {
                float damageCoefficient = Stacks.HighestQuality switch
                {
                    QualityTier.Uncommon => 1.5f,
                    QualityTier.Rare => 2.5f,
                    QualityTier.Epic => 4f,
                    QualityTier.Legendary => 5f,
                    _ => throw new NotImplementedException()
                };

                float damage = Body.damage * damageCoefficient;

                MissileUtils.FireMissile(Body.corePosition, Body, new ProcChainMask(), null, damage, Body.RollCrit(), _missileProjectilePrefab, DamageColorIndex.Item, false);
            }
        }
    }
}
