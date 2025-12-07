using EntityStates;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using RoR2;
using RoR2.Items;
using RoR2.Projectile;
using RoR2.VoidRaidCrab;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ItemQualities.Items
{
    static class ExplodeOnDeath
    {
        public static float GetExplosionRadius(float radius, CharacterBody attacker)
        {
            if (attacker && attacker.inventory)
            {
                ItemQualityCounts explodeOnDeath = ItemQualitiesContent.ItemQualityGroups.ExplodeOnDeath.GetItemCountsEffective(attacker.inventory);
                if (explodeOnDeath.TotalQualityCount > 0)
                {
                    float radiusIncrease = (0.10f * explodeOnDeath.UncommonCount) +
                                           (0.25f * explodeOnDeath.RareCount) +
                                           (0.50f * explodeOnDeath.EpicCount) +
                                           (0.75f * explodeOnDeath.LegendaryCount);

                    if (radiusIncrease > 0)
                    {
                        radius *= 1f + radiusIncrease;
                    }
                }
            }

            return radius;
        }

        [SystemInitializer(typeof(EffectCatalogUtils))]
        static void Init()
        {
            static void enableEffectScale(string effectName)
            {
                EffectIndex effectIndex = EffectCatalogUtils.FindEffectIndex(effectName);
                if (effectIndex == EffectIndex.Invalid)
                {
                    Log.Error($"Failed to find effect '{effectName}'");
                    return;
                }

                EffectComponent effectComponent = EffectCatalog.GetEffectDef(effectIndex)?.prefabEffectComponent;
                if (effectComponent)
                {
                    effectComponent.applyScale = true;
                }
            }

            enableEffectScale("DetonateChargeVFX");
            enableEffectScale("DetonateVFX");

            enableEffectScale("DrifterJunkCubeExplosionVFX");

            IL.EntityStates.Chef.RolyPoly.GearShift += getVisualBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody, false);

            IL.EntityStates.Chef.YesChef.OnEnter += getSimpleEffectDataScaleManipulator(emitGetEntityStateAttackerBody);
            IL.EntityStates.Chef.YesChef.FixedUpdate += groupManipulators(getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody), getSimpleSphereSearchRadiusManipulator(emitGetEntityStateAttackerBody));

            IL.EntityStates.DefectiveUnit.Detonate.OnEnter += getUnscaledEffectDataScaleManipulator(emitGetEntityStateAttackerBody);
            IL.EntityStates.DefectiveUnit.Detonate.FixedUpdate += groupManipulators(getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody), getUnscaledEffectDataScaleManipulator(emitGetEntityStateAttackerBody));

            IL.EntityStates.Drone.DroneBombardment.BombardmentDroneProjectileEffect.ExecuteRadialAttack += groupManipulators(getSimpleSphereSearchRadiusManipulator(emitGetEntityStateAttackerBody), getSimpleEffectDataScaleManipulator(emitGetEntityStateAttackerBody));
            IL.EntityStates.Drone.DroneBombardment.BombardmentDroneSkill.SpawnBombardmentRays += groupManipulators(getSimpleSphereSearchRadiusManipulator(emitGetEntityStateAttackerBody), getSimpleEffectDataScaleManipulator(emitGetEntityStateAttackerBody));

            IL.EntityStates.JellyfishMonster.JellyNova.OnEnter += JellyNova_ReplaceNovaRadius;

            IL.EntityStates.JunkCube.DeathState.Explode += groupManipulators(getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody), getUnscaledEffectDataScaleManipulator(emitGetEntityStateAttackerBody));

            IL.EntityStates.Mage.FlyUpState.OnEnter += getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody);

            IL.EntityStates.Seeker.Meditate.Update += groupManipulators(getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody), getUnscaledEffectDataScaleManipulator(emitGetEntityStateAttackerBody));

            IL.EntityStates.SolusAmalgamator.ShockArmor.OnEnter += getSimpleEffectDataScaleManipulator(emitGetEntityStateAttackerBody);
            IL.EntityStates.SolusAmalgamator.ShockArmor.StartShock += getSimpleEffectDataScaleManipulator(emitGetEntityStateAttackerBody);
            IL.EntityStates.SolusAmalgamator.ShockArmor.ApplyShock += getSimpleBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody);

            IL.EntityStates.VagrantMonster.ChargeMegaNova.OnEnter += ChargeMegaNova_ReplaceNovaRadius;
            IL.EntityStates.VagrantMonster.ChargeMegaNova.FixedUpdate += ChargeMegaNova_ReplaceNovaRadius;

            IL.EntityStates.VagrantNovaItem.ChargeState.OnEnter += VagrantNovaItem_ReplaceBlastRadius;
            IL.EntityStates.VagrantNovaItem.DetonateState.OnEnter += VagrantNovaItem_ReplaceBlastRadius;

            IL.RoR2.FireballVehicle.DetonateServer += getVisualBlastAttackRadiusManipulator(emitGetVehicleSeatPassengerBody);

            IL.RoR2.FissureSlamCracksController.DetonateMeteor += getVisualBlastAttackRadiusManipulator(emitGetBodyComponentBody);

            IL.RoR2.GlobalEventManager.FrozenExplosion += getVisualBlastAttackRadiusManipulator(emitGetMethodParameterBody);

            IL.RoR2.GlobalEventManager.OnHitAllProcess += getVisualBlastAttackRadiusManipulator(emitGetMethodParameterDamageInfoAttackerBody);

            IL.RoR2.GlobalEventManager.ProcIgniteOnKill += groupManipulators(getVisualBlastAttackRadiusManipulator(emitGetMethodParameterDamageReportAttackerBody), getSimpleSphereSearchRadiusManipulator(emitGetMethodParameterDamageReportAttackerBody));

            On.RoR2.Items.JumpDamageStrikeBodyBehavior.GetRadius += JumpDamageStrikeBodyBehavior_GetRadius_ReplaceRadius;

            IL.RoR2.Projectile.ProjectileExplosion.DetonateServer += getVisualBlastAttackRadiusManipulator(emitGetProjectileOwner);

            IL.RoR2.SojournVehicle.EndSojournVehicle += getVisualBlastAttackRadiusManipulator(emitGetVehicleSeatPassengerBody);

            IL.RoR2.VoidRaidCrab.LegController.DoToeConcussionBlastAuthority += getVisualBlastAttackRadiusManipulator(emitGetVoidRaidCrabLegControllerMainBody);

            IL.RoR2.WormBodyPositions2.FireImpactBlastAttack += getVisualBlastAttackRadiusManipulator(emitGetBodyComponentBody);

            On.RoR2.Projectile.DroneBallShootableController.Start += DroneBallShootableController_Start_ReplaceRadius;

            RoR2Application.onLoad += onLoad;
        }

        static void onLoad()
        {
            HashSet<Type> allEntityStateTypes = new HashSet<Type>(EntityStateCatalog.stateIndexToType.Length);

            for (int i = 0; i < EntityStateCatalog.stateIndexToType.Length; i++)
            {
                Type stateType = EntityStateCatalog.stateIndexToType[i];
                while (stateType != null && typeof(EntityState).IsAssignableFrom(stateType) && allEntityStateTypes.Add(stateType))
                {
                    stateType = stateType.BaseType;
                }
            }

            int numAppliedHooks = 0;

            if (allEntityStateTypes.Count > 0)
            {
                ILContext.Manipulator manipulator = getVisualBlastAttackRadiusManipulator(emitGetEntityStateAttackerBody);

                foreach (Type stateType in allEntityStateTypes)
                {
                    try
                    {
                        foreach (MethodInfo method in stateType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                        {
                            ILHook hook = null;
                            try
                            {
                                // The IsGenericMethod call sometimes causes a crash if accessed on a method where an assembly reference can't be resolved,
                                // the DeclaringType getter throws an exception instead, so do that first to catch it before trying to check IsGenericMethod
                                _ = method.DeclaringType;
                                if (method.IsGenericMethod || method.GetMethodBody() == null)
                                    continue;

                                using DynamicMethodDefinition dmd = new DynamicMethodDefinition(method);
                                using ILContext il = new ILContext(dmd.Definition);

                                if (matchSetupBlastAttack(il))
                                {
                                    hook = new ILHook(method, manipulator, new ILHookConfig { ManualApply = true });
                                    hook.Apply();
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Warning($"Failed to apply attack radius hook to {method.DeclaringType.FullName}.{method.Name} ({stateType.Assembly.FullName}): {e.Message}");

                                hook?.Dispose();
                                hook = null;
                            }

                            if (hook != null)
                            {
                                numAppliedHooks++;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warning($"Failed to scan type for attack radius hooks: {stateType.FullName} ({stateType.Assembly.FullName}): {e.Message}");
                    }
                }
            }

            Log.Debug($"Applied {numAppliedHooks} attack radius method hook(s)");
        }

        static bool matchLoadValue(Instruction x, out Instruction instruction)
        {
            if (x.MatchCallOrCallvirt(out _) ||
                x.MatchLdsfld(out _) ||
                x.MatchLdfld(out _) ||
                x.MatchLdloc(out _) ||
                x.MatchLdarg(out _) ||
                x.MatchLdcR4(out _))
            {
                instruction = x;
                return true;
            }

            instruction = null;
            return false;
        }

        static bool instructionsEqual(Instruction a, Instruction b)
        {
            if (a.MatchLdcR4(out float constFloat))
            {
                return b.MatchLdcR4(constFloat);
            }

            if (a.MatchLdarg(out int argIndex))
            {
                return b.MatchLdarg(argIndex);
            }

            if (a.MatchLdloc(out int locIndex))
            {
                return b.MatchLdloc(locIndex);
            }

            if (a.MatchLdfld(out FieldReference fieldA))
            {
                return b.MatchLdfld(out FieldReference fieldB) && fieldA.FullName == fieldB.FullName;
            }

            if (a.MatchLdsfld(out FieldReference staticFieldA))
            {
                return b.MatchLdsfld(out FieldReference staticFieldB) && staticFieldA.FullName == staticFieldB.FullName;
            }

            if (a.MatchCallOrCallvirt(out MethodReference methodA))
            {
                return b.MatchCallOrCallvirt(out MethodReference methodB) && methodA.FullName == methodB.FullName;
            }

            return false;
        }

        static bool matchSetupBlastAttack(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            Instruction loadRadiusValueInstruction = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => matchLoadValue(x, out loadRadiusValueInstruction),
                               x => x.MatchStfld<BlastAttack>(nameof(BlastAttack.radius))))
            {
                return false;
            }

            Func<Instruction, bool>[] setEffectScaleMatch = new Func<Instruction, bool>[]
            {
                x => instructionsEqual(x, loadRadiusValueInstruction),
                x => x.MatchStfld<EffectData>(nameof(EffectData.scale))
            };

            return c.TryGotoNext(setEffectScaleMatch) || c.TryGotoPrev(setEffectScaleMatch);
        }

        // FML
        static void JellyNova_ReplaceNovaRadius(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdsfld<EntityStates.JellyfishMonster.JellyNova>(nameof(EntityStates.JellyfishMonster.JellyNova.novaRadius))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, EntityState, float>>(getRadius);

                static float getRadius(float radius, EntityState entityState)
                {
                    return GetExplosionRadius(radius, entityState?.characterBody);
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }

        static void ChargeMegaNova_ReplaceNovaRadius(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdsfld<EntityStates.VagrantMonster.ChargeMegaNova>(nameof(EntityStates.VagrantMonster.ChargeMegaNova.novaRadius))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, EntityState, float>>(getRadius);

                static float getRadius(float radius, EntityState entityState)
                {
                    return GetExplosionRadius(radius, entityState?.characterBody);
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }

        static void VagrantNovaItem_ReplaceBlastRadius(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdsfld<EntityStates.VagrantNovaItem.DetonateState>(nameof(EntityStates.VagrantNovaItem.DetonateState.blastRadius))))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, EntityState, float>>(getRadius);

                static float getRadius(float radius, EntityState entityState)
                {
                    return GetExplosionRadius(radius, entityState?.characterBody);
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }

        static float JumpDamageStrikeBodyBehavior_GetRadius_ReplaceRadius(On.RoR2.Items.JumpDamageStrikeBodyBehavior.orig_GetRadius orig, JumpDamageStrikeBodyBehavior self, int charge, int stacks)
        {
            return GetExplosionRadius(orig(self, charge, stacks), self.body);
        }

        static void DroneBallShootableController_Start_ReplaceRadius(On.RoR2.Projectile.DroneBallShootableController.orig_Start orig, DroneBallShootableController self)
        {
            if (self &&
                self.TryGetComponent(out ProjectileController projectileController) &&
                projectileController.owner &&
                projectileController.owner.TryGetComponent(out CharacterBody ownerBody))
            {
                self.minRadius = GetExplosionRadius(self.minRadius, ownerBody);
                self.maxRadius = GetExplosionRadius(self.maxRadius, ownerBody);
            }

            orig(self);
        }

        static ILContext.Manipulator groupManipulators(params ILContext.Manipulator[] manipulators)
        {
            return il =>
            {
                foreach (ILContext.Manipulator manipulator in manipulators)
                {
                    manipulator(il);
                }
            };
        }

        static CharacterBody entityStateGetAttackerBody(EntityState entityState)
        {
            if (entityState == null)
                return null;

            if (entityState.projectileController)
            {
                GameObject owner = entityState.projectileController.owner;
                if (owner && owner.TryGetComponent(out CharacterBody ownerBody))
                {
                    return ownerBody;
                }
            }
            else if (entityState.TryGetComponent(out NetworkedBodyAttachment networkedBodyAttachment))
            {
                CharacterBody attachedBody = networkedBodyAttachment.attachedBody;
                if (attachedBody)
                {
                    return attachedBody;
                }
            }
            else if (entityState.TryGetComponent(out GenericOwnership genericOwnership))
            {
                GameObject owner = genericOwnership.ownerObject;
                if (owner && owner.TryGetComponent(out CharacterBody ownerBody))
                {
                    return ownerBody;
                }
            }
            else if (entityState.TryGetComponent(out DroneCommandReceiver droneCommandReceiver))
            {
                CharacterBody leaderBody = droneCommandReceiver.leaderBody;
                if (leaderBody)
                {
                    return leaderBody;
                }
            }
            else if (entityState.TryGetComponent(out DestructibleSpawerDynamiteController destructibleSpawerDynamiteController))
            {
                GameObject owner = destructibleSpawerDynamiteController.owner;
                if (owner && owner.TryGetComponent(out CharacterBody ownerBody))
                {
                    return ownerBody;
                }
            }
            else if (entityState.TryGetComponent(out JunkCubeController junkCubeController))
            {
                GameObject owner = junkCubeController.owner;
                if (owner && owner.TryGetComponent(out CharacterBody ownerBody))
                {
                    return ownerBody;
                }
            }

            return entityState.characterBody;
        }

        static void emitGetEntityStateAttackerBody(ILCursor c)
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<EntityState, CharacterBody>>(entityStateGetAttackerBody);
        }

        static T tryGetAsComponent<T>(Component component) where T : Component
        {
            if (component)
            {
                if (component is T tComponent || component.TryGetComponent(out tComponent))
                    return tComponent;
            }

            return null;
        }

        static void emitGetVehicleSeatPassengerBody(ILCursor c)
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<MonoBehaviour, CharacterBody>>(getPassenger);
            
            static CharacterBody getPassenger(MonoBehaviour component)
            {
                VehicleSeat vehicleSeat = tryGetAsComponent<VehicleSeat>(component);
                return vehicleSeat ? vehicleSeat.currentPassengerBody : null;
            }
        }

        static void emitGetBodyComponentBody(ILCursor c)
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<MonoBehaviour, CharacterBody>>(getBody);

            static CharacterBody getBody(MonoBehaviour component)
            {
                return tryGetAsComponent<CharacterBody>(component);
            }
        }

        static void emitGetMethodParameterBody(ILCursor c)
        {
            if (c.Context.Method.TryFindParameter<CharacterBody>(out ParameterDefinition bodyParameter))
            {
                c.Emit(OpCodes.Ldarg, bodyParameter);
            }
            else
            {
                Log.Error($"Failed to find body parameter for {c.Context.Method.FullName}");
                c.Emit(OpCodes.Ldnull);
            }
        }

        static void emitGetMethodParameterDamageInfoAttackerBody(ILCursor c)
        {
            if (c.Context.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                c.Emit(OpCodes.Ldarg, damageInfoParameter);
                c.EmitDelegate<Func<DamageInfo, CharacterBody>>(getAttackerBody);

                static CharacterBody getAttackerBody(DamageInfo damageInfo)
                {
                    return damageInfo?.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                }
            }
            else
            {
                Log.Error($"Failed to find DamageInfo parameter for {c.Context.Method.FullName}");
                c.Emit(OpCodes.Ldnull);
            }
        }

        static void emitGetMethodParameterDamageReportAttackerBody(ILCursor c)
        {
            FieldInfo attackerBodyField = typeof(DamageReport).GetField(nameof(DamageReport.attackerBody), BindingFlags.Public | BindingFlags.Instance);
            if (attackerBodyField == null)
            {
                Log.Warning("Failed to find DamageReport.attackerBody field");
            }

            if (c.Context.Method.TryFindParameter<DamageReport>(out ParameterDefinition damageReportParameter))
            {
                c.Emit(OpCodes.Ldarg, damageReportParameter);
                if (attackerBodyField != null)
                {
                    c.Emit(OpCodes.Ldfld, attackerBodyField);
                }
                else
                {
                    c.EmitDelegate<Func<DamageReport, CharacterBody>>(getAttackerBody);

                    static CharacterBody getAttackerBody(DamageReport damageReport)
                    {
                        return damageReport?.attackerBody;
                    }
                }
            }
            else
            {
                Log.Error($"Failed to find DamageReport parameter for {c.Context.Method.FullName}");
                c.Emit(OpCodes.Ldnull);
            }
        }

        static void emitGetProjectileOwner(ILCursor c)
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<MonoBehaviour, CharacterBody>>(getBody);

            static CharacterBody getBody(MonoBehaviour component)
            {
                ProjectileController projectileController = tryGetAsComponent<ProjectileController>(component);
                if (!projectileController || !projectileController.owner)
                    return null;

                return projectileController.owner.GetComponent<CharacterBody>();
            }
        }

        static void emitGetVoidRaidCrabLegControllerMainBody(ILCursor c)
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<MonoBehaviour, CharacterBody>>(getBody);

            static CharacterBody getBody(MonoBehaviour component)
            {
                LegController legController = tryGetAsComponent<LegController>(component);
                return legController ? legController.mainBody : null;
            }
        }

        static ILContext.Manipulator getVisualBlastAttackRadiusManipulator(Action<ILCursor> emitGetAttackerBody, bool strictRadiusMatch = true)
        {
            return il =>
            {
                visualBlastAttackRadiusManipulator(il, emitGetAttackerBody, strictRadiusMatch);
            };
        }

        static ILContext.Manipulator getSimpleBlastAttackRadiusManipulator(Action<ILCursor> emitGetAttackerBody)
        {
            return il =>
            {
                simpleBlastAttackRadiusManipulator(il, emitGetAttackerBody);
            };
        }

        static ILContext.Manipulator getSimpleSphereSearchRadiusManipulator(Action<ILCursor> emitGetAttackerBody)
        {
            return il =>
            {
                simpleSphereSearchRadiusManipulator(il, emitGetAttackerBody);
            };
        }

        static ILContext.Manipulator getSimpleEffectDataScaleManipulator(Action<ILCursor> emitGetAttackerBody)
        {
            return il =>
            {
                simpleEffectDataScaleManipulator(il, emitGetAttackerBody);
            };
        }

        static ILContext.Manipulator getUnscaledEffectDataScaleManipulator(Action<ILCursor> emitGetAttackerBody)
        {
            return il =>
            {
                unscaledEffectDataScaleManipulator(il, emitGetAttackerBody);
            };
        }

        static void visualBlastAttackRadiusManipulator(ILContext il, Action<ILCursor> emitGetAttackerBody, bool strictRadiusMatch = true)
        {
            ILCursor c = new ILCursor(il);

            HashSet<Instruction> patchedEffectDataScaleSetters = new HashSet<Instruction>();

            Instruction loadRadiusValueInstruction = null;

            Func<Instruction, bool>[] setEffectScaleMatch = new Func<Instruction, bool>[]
            {
                x => !strictRadiusMatch || instructionsEqual(x, loadRadiusValueInstruction),
                x => x.MatchStfld<EffectData>(nameof(EffectData.scale)) && !patchedEffectDataScaleSetters.Contains(x)
            };

            int patchCount = 0;
            int effectDataPatchCount = 0;
            while (c.TryGotoNext(MoveType.After,
                                 x => matchLoadValue(x, out loadRadiusValueInstruction),
                                 x => x.MatchStfld<BlastAttack>(nameof(BlastAttack.radius))))
            {
                ILCursor effectDataCursor = c.Clone();

                if (effectDataCursor.TryGotoNext(MoveType.After, setEffectScaleMatch) ||
                    effectDataCursor.TryGotoPrev(MoveType.After, setEffectScaleMatch))
                {
                    // move before set scale
                    effectDataCursor.Index--;

                    patchedEffectDataScaleSetters.Add(effectDataCursor.Next);

                    emitGetAttackerBody(effectDataCursor);
                    effectDataCursor.EmitDelegate<Func<float, CharacterBody, float>>(GetExplosionRadius);

                    effectDataPatchCount++;
                }

                // move before set radius
                c.Index--;

                emitGetAttackerBody(c);
                c.EmitDelegate<Func<float, CharacterBody, float>>(GetExplosionRadius);

                patchCount++;

                c.SearchTarget = SearchTarget.Next;
            }

            if (patchCount == 0 || effectDataPatchCount != patchCount)
            {
                Log.Error($"{il.Method.FullName}: Failed to find valid patch location(s) (found {patchCount} radius location(s), {effectDataPatchCount} effect radius location(s))");
            }
            else
            {
                Log.Debug($"{il.Method.FullName}: Found {patchCount} radius and {effectDataPatchCount} effect radius patch location(s)");
            }
        }

        static void simpleBlastAttackRadiusManipulator(ILContext il, Action<ILCursor> emitGetAttackerBody)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;
            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchStfld<BlastAttack>(nameof(BlastAttack.radius))))
            {
                emitGetAttackerBody(c);
                c.EmitDelegate<Func<float, CharacterBody, float>>(GetExplosionRadius);

                patchCount++;

                c.SearchTarget = SearchTarget.Next;
            }

            if (patchCount == 0)
            {
                Log.Error($"{il.Method.FullName}: Failed to find patch location");
            }
            else
            {
                Log.Debug($"{il.Method.FullName}: Found {patchCount} patch location(s)");
            }
        }

        static void simpleSphereSearchRadiusManipulator(ILContext il, Action<ILCursor> emitGetAttackerBody)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;
            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchStfld<SphereSearch>(nameof(SphereSearch.radius))))
            {
                emitGetAttackerBody(c);
                c.EmitDelegate<Func<float, CharacterBody, float>>(GetExplosionRadius);

                patchCount++;

                c.SearchTarget = SearchTarget.Next;
            }

            if (patchCount == 0)
            {
                Log.Error($"{il.Method.FullName}: Failed to find patch location");
            }
            else
            {
                Log.Debug($"{il.Method.FullName}: Found {patchCount} patch location(s)");
            }
        }

        static void simpleEffectDataScaleManipulator(ILContext il, Action<ILCursor> emitGetAttackerBody)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;
            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchStfld<EffectData>(nameof(EffectData.scale))))
            {
                emitGetAttackerBody(c);
                c.EmitDelegate<Func<float, CharacterBody, float>>(GetExplosionRadius);

                patchCount++;

                c.SearchTarget = SearchTarget.Next;
            }

            if (patchCount == 0)
            {
                Log.Error($"{il.Method.FullName}: Failed to find patch location");
            }
            else
            {
                Log.Debug($"{il.Method.FullName}: Found {patchCount} patch location(s)");
            }
        }

        static void unscaledEffectDataScaleManipulator(ILContext il, Action<ILCursor> emitGetAttackerBody)
        {
            FieldInfo effectDataScaleField = typeof(EffectData).GetField(nameof(EffectData.scale), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (effectDataScaleField == null)
            {
                Log.Error("Failed to find EffectData.scale field");
                return;
            }

            ILCursor c = new ILCursor(il);

            int patchCount = 0;
            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchNewobj<EffectData>()))
            {
                c.Emit(OpCodes.Dup);
                c.Emit(OpCodes.Dup);
                c.Emit(OpCodes.Ldfld, effectDataScaleField);
                emitGetAttackerBody(c);
                c.EmitDelegate<Func<float, CharacterBody, float>>(GetExplosionRadius);
                c.Emit(OpCodes.Stfld, effectDataScaleField);

                patchCount++;

                c.SearchTarget = SearchTarget.Next;
            }

            if (patchCount == 0)
            {
                Log.Error($"{il.Method.FullName}: Failed to find patch location");
            }
            else
            {
                Log.Debug($"{il.Method.FullName}: Found {patchCount} patch location(s)");
            }
        }
    }
}
