using HG;
using ItemQualities.ContentManagement;
using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ItemQualities.Equipments
{
    internal static class Saw
    {
        private static readonly GameObject[] _qualitySawProjectilePrefabs = new GameObject[(int)QualityTier.Count];

        [ContentInitializer]
        private static IEnumerator LoadContent(ContentInitializerArgs args)
        {
            AsyncOperationHandle<GameObject> sawProjectileLoad = AddressableUtil.LoadTempAssetAsync<GameObject>(RoR2_Base_Saw.Sawmerang_prefab);
            sawProjectileLoad.OnSuccess(sawProjectilePrefab =>
            {
                for (QualityTier qualityTier = 0; qualityTier < QualityTier.Count; qualityTier++)
                {
                    GameObject qualitySawProjectilePrefab = sawProjectilePrefab.InstantiateClone(sawProjectilePrefab.name + qualityTier.ToString());

                    BoomerangProjectileQualityController qualityController = qualitySawProjectilePrefab.EnsureComponent<BoomerangProjectileQualityController>();
                    qualityController.HitPauseDuration = 1f;

                    _qualitySawProjectilePrefabs[(int)qualityTier] = qualitySawProjectilePrefab;
                }
            });

            return sawProjectileLoad.AsProgressCoroutine(args.ProgressReceiver);
        }

        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.EquipmentSlot.FireSaw += EquipmentSlot_FireSaw;

            MethodInfo fireSawMethod = typeof(EquipmentSlot).GetMethod(nameof(EquipmentSlot.FireSaw), ReflectionUtil.AllFlags);
            if (fireSawMethod != null)
            {
                using DynamicMethodDefinition dmd = new DynamicMethodDefinition(fireSawMethod);
                using ILContext il = new ILContext(dmd.Definition);
                ILCursor c = new ILCursor(il);

                MethodReference fireSingleSawMethodRef = null;
                if (!c.TryGotoNext(MoveType.Before,
                                   x => x.MatchCallOrCallvirt(out fireSingleSawMethodRef) && fireSingleSawMethodRef != null && fireSingleSawMethodRef.Name.StartsWith("<FireSaw>g__FireSingleSaw|")))
                {
                    Log.Error("Failed to find FireSaw FireSingleSaw local method");
                }
                else
                {
                    MethodBase fireSingleSawMethod;
                    try
                    {
                        fireSingleSawMethod = fireSingleSawMethodRef?.ResolveReflection();
                    }
                    catch (Exception)
                    {
                        fireSingleSawMethod = null;
                    }

                    if (fireSingleSawMethod == null)
                    {
                        Log.Error("Failed to resolve FireSaw FireSingleSaw local method");
                    }
                    else
                    {
                        new ILHook(fireSingleSawMethod, EquipmentSlot_FireSaw_FireSingleSaw);
                    }
                }
            }
            else
            {
                Log.Error("Failed to find EquipmentSlot.FireSaw method");
            }
        }

        private static void EquipmentSlot_FireSaw_FireSingleSaw(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdstr("Prefabs/Projectiles/Sawmerang"),
                               x => x.MatchCallOrCallvirt(typeof(LegacyResourcesAPI), nameof(LegacyResourcesAPI.Load))))
            {
                Log.Error("Failed to find saw projectile prefab patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<GameObject, EquipmentSlot, GameObject>>(getProjectilePrefab);

            static GameObject getProjectilePrefab(GameObject prefab, EquipmentSlot equipmentSlot)
            {
                QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();
                GameObject qualityPrefab = ArrayUtils.GetSafe(_qualitySawProjectilePrefabs, (int)qualityTier);

                return qualityPrefab ? qualityPrefab : prefab;
            }
        }

        private static void EquipmentSlot_FireSaw(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            // IL_0044: ldarg.0
            // IL_0045: ldarg.0
            // IL_0046: call      instance class RoR2.CharacterBody RoR2.EquipmentSlot::get_characterBody()
            // IL_004B: ldloca.s  V_0
            // IL_004D: call      instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Ray::get_origin()
            // IL_0052: ldloc.1
            // IL_0053: call      instance void RoR2.EquipmentSlot::'<FireSaw>g__FireSingleSaw|96_0'(class RoR2.CharacterBody, valuetype [UnityEngine.CoreModule]UnityEngine.Vector3, valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion)
            //     this.<FireSaw>g__FireSingleSaw|96_0(this.characterBody, aimRay.origin, quaternion * Quaternion.Euler(0f, num, 0f));

            VariableDefinition sawRotationVar = null;
            Instruction afterFireMiddleSawInstruction = null;
            if (!c.TryGotoNext(MoveType.AfterLabel,
                               x => x.MatchLdarg(0),
                               x => x.MatchLdarg(0),
                               x => x.MatchCallOrCallvirt<EquipmentSlot>("get_" + nameof(EquipmentSlot.characterBody)),
                               x => x.MatchLdloca(out _), // aimRay
                               x => x.MatchCallOrCallvirt<Ray>("get_" + nameof(Ray.origin)),
                               x => x.MatchLdloc<Quaternion>(il, out sawRotationVar),
                               x => x.MatchCallOrCallvirt(out _), // call FireSingleSaw
                               x => x.MatchAny(out afterFireMiddleSawInstruction)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            VariableDefinition sawAngleVar = null;
            if (!c.Clone().TryGotoNext(x => x.MatchLdcR4(out _),
                                       x => x.MatchLdloc<float>(il, out sawAngleVar),
                                       x => x.MatchLdcR4(out _),
                                       x => x.MatchCallOrCallvirt<Quaternion>(nameof(Quaternion.Euler))))
            {
                Log.Warning("Failed to find sawAngle variable");
            }

            VariableDefinition numMiddleSawsVar = il.AddVariable<int>();
            VariableDefinition rotationPerSawVar = il.AddVariable<Quaternion>();

            VariableDefinition originalSawRotationVar = il.AddVariable<Quaternion>();
            c.Emit(OpCodes.Ldloc, sawRotationVar);
            c.Emit(OpCodes.Stloc, originalSawRotationVar);

            c.Emit(OpCodes.Ldarg_0);

            if (sawAngleVar != null)
            {
                c.Emit(OpCodes.Ldloc, sawAngleVar);
            }
            else
            {
                c.Emit(OpCodes.Ldc_R4, 15f);
            }

            c.Emit(OpCodes.Ldloca, numMiddleSawsVar);
            c.Emit(OpCodes.Ldloca, rotationPerSawVar);
            c.Emit(OpCodes.Ldloca, sawRotationVar);
            c.EmitDelegate<GetSawLoopParamsDelegate>(getSawLoopParams);

            static void getSawLoopParams(EquipmentSlot equipmentSlot, float sawAngle, out int middleSawRepeatCount, out Quaternion rotationPerSaw, ref Quaternion sawRotation)
            {
                int extraSawCount;

                QualityTier qualityTier = equipmentSlot.GetCurrentEquipmentActionQualityTier();
                switch (qualityTier)
                {
                    case QualityTier.None:
                        extraSawCount = 0;
                        break;
                    case QualityTier.Uncommon:
                        extraSawCount = 1;
                        break;
                    case QualityTier.Rare:
                        extraSawCount = 2;
                        break;
                    case QualityTier.Epic:
                        extraSawCount = 3;
                        break;
                    case QualityTier.Legendary:
                        extraSawCount = 5;
                        break;
                    default:
                        extraSawCount = 0;
                        Log.Warning($"Quality tier {qualityTier} is not implemented");
                        break;
                }

                middleSawRepeatCount = 1 + extraSawCount;

                rotationPerSaw = Quaternion.Euler(0f, (sawAngle * 2f) / (middleSawRepeatCount + 1), 0f);

                sawRotation *= Quaternion.Euler(0f, -sawAngle, 0f);
            }

            ILLabel loopStartLabel = c.MarkLabel();
            ILLabel loopEndLabel = c.DefineLabel();

            c.Emit(OpCodes.Ldloc, numMiddleSawsVar);
            c.Emit(OpCodes.Ldc_I4_0);
            c.Emit(OpCodes.Ble, loopEndLabel);

            c.Emit(OpCodes.Ldloc, sawRotationVar);
            c.Emit(OpCodes.Ldloc, rotationPerSawVar);
            c.EmitDelegate<Func<Quaternion, Quaternion, Quaternion>>(getNextSawRotation);
            c.Emit(OpCodes.Stloc, sawRotationVar);

            static Quaternion getNextSawRotation(Quaternion sawRotation, Quaternion rotationPerSaw)
            {
                return sawRotation * rotationPerSaw;
            }

            c.Goto(afterFireMiddleSawInstruction, MoveType.Before);

            c.Emit(OpCodes.Ldloc, numMiddleSawsVar);
            c.Emit(OpCodes.Ldc_I4_1);
            c.Emit(OpCodes.Sub);
            c.Emit(OpCodes.Stloc, numMiddleSawsVar);

            c.Emit(OpCodes.Br, loopStartLabel);

            c.MarkLabel(loopEndLabel);

            c.Emit(OpCodes.Ldloc, originalSawRotationVar);
            c.Emit(OpCodes.Stloc, sawRotationVar);
        }

        private delegate void GetSawLoopParamsDelegate(EquipmentSlot equipmentSlot, float sawAngle, out int middleSawRepeatCount, out Quaternion rotationPerSaw, ref Quaternion sawRotation);
    }
}
