using HG;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

using DeathProjectileComponent = RoR2.Projectile.DeathProjectile;

namespace ItemQualities.Equipments
{
    internal static class DeathProjectile
    {
        private static readonly GameObject[] _qualityDeathProjectilePrefabs = new GameObject[(int)QualityTier.Count];

        private static BuffIndex[] _validEliteBuffIndices = Array.Empty<BuffIndex>();

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> deathProjectileLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_DeathProjectile.DeathProjectile_prefab);
            deathProjectileLoad.OnSuccess(deathProjectilePrefab =>
            {
                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    float durationIncrease;
                    switch (qualityTier)
                    {
                        case QualityTier.Uncommon:
                            durationIncrease = 1f;
                            break;
                        case QualityTier.Rare:
                            durationIncrease = 3f;
                            break;
                        case QualityTier.Epic:
                            durationIncrease = 5f;
                            break;
                        case QualityTier.Legendary:
                            durationIncrease = 8f;
                            break;
                        default:
                            throw new NotImplementedException($"Quality tier {qualityTier} is not implemented");
                    }

                    GameObject qualityDeathProjectilePrefab = deathProjectilePrefab.InstantiateClone(deathProjectilePrefab.name + qualityTier.ToString());

                    DeathProjectileComponent qualityDeathProjectileComponent = qualityDeathProjectilePrefab.GetComponent<DeathProjectileComponent>();
                    qualityDeathProjectileComponent.baseDuration += durationIncrease;

                    QualityTierContext qualityTierContext = qualityDeathProjectilePrefab.AddComponent<QualityTierContext>();
                    qualityTierContext.QualityTier = qualityTier;

                    GameObject.Destroy(qualityDeathProjectilePrefab.GetComponent<DestroyOnTimer>());

                    _qualityDeathProjectilePrefabs[(int)qualityTier] = qualityDeathProjectilePrefab;
                }

                args.ContentPack.networkedObjectPrefabs.Add(_qualityDeathProjectilePrefabs);
                args.ContentPack.bodyPrefabs.Add(_qualityDeathProjectilePrefabs);
                args.ContentPack.projectilePrefabs.Add(_qualityDeathProjectilePrefabs);
            });

            return deathProjectileLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer(typeof(BuffCatalog))]
        private static void Init()
        {
            IL.RoR2.EquipmentSlot.FireDeathProjectile += EquipmentSlot_FireDeathProjectile;

            IL.RoR2.Projectile.DeathProjectile.FixedUpdate += DeathProjectile_FixedUpdate;

            using var _ = SetPool<BuffIndex>.RentCollection(out HashSet<BuffIndex> validEliteBuffsList);

            foreach (BuffIndex buffIndex in BuffCatalog.eliteBuffIndices)
            {
                BuffDef buffDef = BuffCatalog.GetBuffDef(buffIndex);
                if (!buffDef || !buffDef.eliteDef)
                    continue;

                string modifierToken = buffDef.eliteDef.modifierToken;
                if (!string.IsNullOrWhiteSpace(modifierToken) && !Language.IsTokenInvalid(modifierToken))
                {
                    validEliteBuffsList.Add(buffIndex);
                }
                else
                {
                    Log.Debug($"Excluding elite buff {buffDef.name}");
                }
            }

            if (validEliteBuffsList.Count > 0)
            {
                _validEliteBuffIndices = validEliteBuffsList.ToArray();
                Array.Sort(_validEliteBuffIndices);
            }
        }

        private static void EquipmentSlot_FireDeathProjectile(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdstr("Prefabs/Projectiles/DeathProjectile"),
                               x => x.MatchCallOrCallvirt(typeof(LegacyResourcesAPI), nameof(LegacyResourcesAPI.Load))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<GameObject, EquipmentSlot, GameObject>>(getProjectilePrefab);

            static GameObject getProjectilePrefab(GameObject prefab, EquipmentSlot equipmentSlot)
            {
                QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();
                if (qualityTier > QualityTier.None)
                {
                    GameObject qualityPrefab = ArrayUtils.GetSafe(_qualityDeathProjectilePrefabs, (int)qualityTier);
                    if (qualityPrefab)
                    {
                        prefab = qualityPrefab;
                    }
                }

                return prefab;
            }
        }

        private static void DeathProjectile_FixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<DeathProjectileComponent>(nameof(DeathProjectileComponent.SpawnTickEffect))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Action<DeathProjectileComponent>>(onTick);

                static void onTick(DeathProjectileComponent deathProjectileComponent)
                {
                    if (_validEliteBuffIndices.Length == 0)
                        return;

                    if (!deathProjectileComponent)
                        return;

                    if (!deathProjectileComponent.healthComponent || !deathProjectileComponent.healthComponent.body)
                        return;

                    if (!deathProjectileComponent.projectileController)
                        return;

                    if (deathProjectileComponent.TryGetComponentCached(out QualityTierContext qualityTierContext))
                    {
                        float eliteChance;

                        QualityTier qualityTier = qualityTierContext.QualityTier;
                        switch (qualityTier)
                        {
                            case QualityTier.None:
                                eliteChance = 0f;
                                break;
                            case QualityTier.Uncommon:
                                eliteChance = 5f;
                                break;
                            case QualityTier.Rare:
                                eliteChance = 10f;
                                break;
                            case QualityTier.Epic:
                                eliteChance = 20f;
                                break;
                            case QualityTier.Legendary:
                                eliteChance = 33f;
                                break;
                            default:
                                eliteChance = 0f;
                                Log.Warning($"Quality tier {qualityTier} is not implemented");
                                break;
                        }

                        if (eliteChance > 0f)
                        {
                            CharacterBody ownerBody = deathProjectileComponent.projectileController.owner ? deathProjectileComponent.projectileController.owner.GetComponent<CharacterBody>() : null;
                            CharacterMaster ownerMaster = ownerBody ? ownerBody.master : null;

                            bool sureProc = deathProjectileComponent.projectileController.procChainMask.HasProc(ProcType.SureProc);

                            if (RollUtil.CheckRoll(eliteChance, ownerMaster, sureProc))
                            {
                                BuffIndex eliteBuffIndex = RoR2Application.rng.NextElementUniform(_validEliteBuffIndices);

                                Log.Debug($"Selected elite buff {BuffCatalog.GetBuffDef(eliteBuffIndex)}");

                                deathProjectileComponent.healthComponent.body.AddTimedBuff(eliteBuffIndex, 0.5f);

                                // Need to do immediate stats recalc for the buff to apply in time for the death event
                                deathProjectileComponent.healthComponent.body.RecalculateStats();
                            }
                        }
                    }
                }
            }
            else
            {
                Log.Error("Failed to find elite buff patch location");
            }

            c.Goto(0, MoveType.Before);

            VariableDefinition damageReportVar = null;
            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchLdsfld<GlobalEventManager>(nameof(GlobalEventManager.instance)),
                              x => x.MatchLdloc<DamageReport>(il, out damageReportVar),
                              x => x.MatchCallOrCallvirt<GlobalEventManager>(nameof(GlobalEventManager.OnCharacterDeath))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, damageReportVar);
                c.EmitDelegate<Action<DeathProjectileComponent, DamageReport>>(modifyDeathReport);

                static void modifyDeathReport(DeathProjectileComponent deathProjectileComponent, DamageReport damageReport)
                {
                    QualityTier qualityTier = QualityTierContext.GetQualityTier(deathProjectileComponent.gameObject);
                    if (qualityTier != QualityTier.None)
                    {
                        damageReport.damageInfo.damageType.AddModdedDamageType(DamageTypes.BypassDrops);
                    }
                }
            }
            else
            {
                Log.Error("Failed to find damage report patch location");
            }
        }
    }
}
