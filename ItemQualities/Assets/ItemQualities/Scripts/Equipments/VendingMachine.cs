using HG;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Equipments
{
    static class VendingMachine
    {
        static readonly GameObject[] _qualityVendingMachineProjectilePrefabs = new GameObject[(int)QualityTier.Count];

        [ContentInitializer]
        static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> vendingMachineProjectileLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_DLC1_VendingMachine.VendingMachineProjectile_prefab);
            vendingMachineProjectileLoad.OnSuccess(vendingMachineProjectilePrefab =>
            {
                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    GameObject qualityVendingMachineProjectilePrefab = vendingMachineProjectilePrefab.InstantiateClone(vendingMachineProjectilePrefab.name + qualityTier);

                    if (qualityVendingMachineProjectilePrefab.TryGetComponent(out ProjectileInstantiateDeployable projectileInstantiateDeployable))
                    {
                        projectileInstantiateDeployable.prefab = projectileInstantiateDeployable.prefab.InstantiateClone(projectileInstantiateDeployable.prefab.name + qualityTier);

                        InteractOnTimer interactOnTimer = projectileInstantiateDeployable.prefab.EnsureComponent<InteractOnTimer>();
                        interactOnTimer.InteractInterval = qualityTier switch
                        {
                            QualityTier.Uncommon => 15f,
                            QualityTier.Rare => 8f,
                            QualityTier.Epic => 4f,
                            QualityTier.Legendary => 1f,
                            _ => throw new NotImplementedException($"Quality tier {qualityTier} is not implemented")
                        };

                        args.ContentPack.networkedObjectPrefabs.Add(projectileInstantiateDeployable.prefab);
                    }
                    else
                    {
                        Log.Error($"{qualityVendingMachineProjectilePrefab.name} is missing ProjectileInstantiateDeployable component");
                    }

                    args.ContentPack.projectilePrefabs.Add(qualityVendingMachineProjectilePrefab);

                    _qualityVendingMachineProjectilePrefabs[(int)qualityTier] = qualityVendingMachineProjectilePrefab;
                }
            });

            return vendingMachineProjectileLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.EquipmentSlot.FireVendingMachine += EquipmentSlot_FireVendingMachine;
        }

        static void EquipmentSlot_FireVendingMachine(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdstr("Prefabs/Projectiles/VendingMachineProjectile"),
                               x => x.MatchCallOrCallvirt(typeof(LegacyResourcesAPI), nameof(LegacyResourcesAPI.Load))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<GameObject, EquipmentSlot, GameObject>>(getProjectilePrefab);

            static GameObject getProjectilePrefab(GameObject projectilePrefab, EquipmentSlot equipmentSlot)
            {
                QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();

                GameObject qualityProjectilePrefab = ArrayUtils.GetSafe(_qualityVendingMachineProjectilePrefabs, (int)qualityTier);
                if (qualityProjectilePrefab)
                {
                    projectilePrefab = qualityProjectilePrefab;
                }

                return projectilePrefab;
            }
        }
    }
}
