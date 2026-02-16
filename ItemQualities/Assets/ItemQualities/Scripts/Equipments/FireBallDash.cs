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
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Equipments
{
    static class FireBallDash
    {
        static readonly GameObject[] _qualityVehiclePrefabs = new GameObject[(int)QualityTier.Count];

        [ContentInitializer]
        static IEnumerator LoadContent(ContentIntializerArgs args)
        {
            AsyncOperationHandle<GameObject> fireballVehicleLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_FireBallDash.FireballVehicle_prefab);
            fireballVehicleLoad.OnSuccess(fireballVehiclePrefab =>
            {
                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    float blastDamageCoefficientBonusPerHit;
                    float blastRadiusBonusPerHit;
                    switch (qualityTier)
                    {
                        case QualityTier.Uncommon:
                            blastDamageCoefficientBonusPerHit = 0.2f;
                            blastRadiusBonusPerHit = 0.5f;
                            break;
                        case QualityTier.Rare:
                            blastDamageCoefficientBonusPerHit = 0.5f;
                            blastRadiusBonusPerHit = 1.0f;
                            break;
                        case QualityTier.Epic:
                            blastDamageCoefficientBonusPerHit = 1.0f;
                            blastRadiusBonusPerHit = 1.5f;
                            break;
                        case QualityTier.Legendary:
                            blastDamageCoefficientBonusPerHit = 2.0f;
                            blastRadiusBonusPerHit = 2.5f;
                            break;
                        default:
                            throw new NotImplementedException($"Quality tier {qualityTier} is not implemented");
                    }

                    GameObject qualityFireballVehiclePrefab = fireballVehiclePrefab.InstantiateClone(fireballVehiclePrefab.name + qualityTier.ToString());

                    FireballVehicleQualityController qualityController = qualityFireballVehiclePrefab.EnsureComponent<FireballVehicleQualityController>();
                    qualityController.BlastDamageCoefficientBonusPerHit = blastDamageCoefficientBonusPerHit;
                    qualityController.BlastRadiusBonusPerHit = blastRadiusBonusPerHit;

                    _qualityVehiclePrefabs[(int)qualityTier] = qualityFireballVehiclePrefab;
                }
            });

            return fireballVehicleLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.EquipmentSlot.FireFireBallDash += EquipmentSlot_FireFireBallDash;

            IL.RoR2.FireballVehicle.FixedUpdate += FireballVehicle_FixedUpdate;
        }

        static void EquipmentSlot_FireFireBallDash(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdstr("Prefabs/NetworkedObjects/FireballVehicle"),
                               x => x.MatchCallOrCallvirt(typeof(LegacyResourcesAPI), nameof(LegacyResourcesAPI.Load))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<GameObject, EquipmentSlot, GameObject>>(getVehiclePrefab);

            static GameObject getVehiclePrefab(GameObject prefab, EquipmentSlot equipmentSlot)
            {
                QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();
                if (qualityTier > QualityTier.None)
                {
                    GameObject qualityPrefab = ArrayUtils.GetSafe(_qualityVehiclePrefabs, (int)qualityTier);
                    if (qualityPrefab)
                    {
                        prefab = qualityPrefab;
                    }
                }

                return prefab;
            }
        }

        static void FireballVehicle_FixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<OverlapAttack>(nameof(OverlapAttack.Fire)),
                               x => x.MatchBrfalse(out _)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<FireballVehicle>>(onOverlapAttackHit);

            static void onOverlapAttackHit(FireballVehicle fireballVehicle)
            {
                if (fireballVehicle.TryGetComponentCached(out FireballVehicleQualityController qualityController))
                {
                    qualityController.OnOverlapAttackHitServer();
                }
            }
        }
    }
}
