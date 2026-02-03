using HG;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using R2API.Utils;
using RoR2;
using RoR2BepInExPack.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ItemQualities.Equipments
{
    static class EquipmentHooks
    {
        static readonly FixedConditionalWeakTable<EquipmentSlot, List<EquipmentAction>> _equipmentActionsBySlot = new();
        internal sealed class EquipmentAction
        {
            public EquipmentSlot EquipmentSlot { get; }

            public QualityTier QualityTier { get; }

            public EquipmentAction(EquipmentSlot equipmentSlot, QualityTier qualityTier)
            {
                EquipmentSlot = equipmentSlot;
                QualityTier = qualityTier;
            }
        }

        public static bool TryGetCurrentEquipmentAction(EquipmentSlot equipmentSlot, out EquipmentAction action)
        {
            if (_equipmentActionsBySlot.TryGetValue(equipmentSlot, out List<EquipmentAction> actions) && actions.Count > 0)
            {
                action = actions[^1];
                return true;
            }

            action = default;
            return false;
        }

        [InitDuringStartupPhase(GameInitPhase.PreFrame)]
        static void PreInit()
        {
            SystemInitializerInjector.InjectDependency(typeof(RuleCatalog), typeof(QualityCatalog));

            IL.RoR2.RuleCatalog.Init += qualityEquipmentCanDropPatch;
        }

        [SystemInitializer(typeof(CostTypeCatalog))]
        static void Init()
        {
            MethodInfo performEquipmentActionMethod = typeof(EquipmentSlot).GetMethod(nameof(EquipmentSlot.PerformEquipmentAction), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (performEquipmentActionMethod != null)
            {
                HookConfig config = new HookConfig
                {
                    Priority = 100
                };

                new Hook(performEquipmentActionMethod, new On.RoR2.EquipmentSlot.hook_PerformEquipmentAction(EquipmentSlot_PerformEquipmentAction), config);
            }
            else
            {
                Log.Error("Failed to find EquipmentSlot.PerformEquipmentAction method");
            }

            On.RoR2.CharacterModel.SetEquipmentDisplay += CharacterModel_SetEquipmentDisplay;
            On.RoR2.CharacterModel.HighlightEquipentDisplay += CharacterModel_HighlightEquipentDisplay;

            MethodInfo currentEquipmentIndexGetter = typeof(Inventory).GetProperty(nameof(Inventory.currentEquipmentIndex))?.GetMethod;
            if (currentEquipmentIndexGetter != null)
            {
                new Hook(currentEquipmentIndexGetter, new hook_Inventory_get_currentEquipmentIndex(Inventory_get_currentEquipmentIndex));
            }
            else
            {
                Log.Error("Failed to find Inventory.currentEquipmentIndex getter method");
            }

            MethodInfo alternateEquipmentIndexGetter = typeof(Inventory).GetProperty(nameof(Inventory.alternateEquipmentIndex))?.GetMethod;
            if (alternateEquipmentIndexGetter != null)
            {
                new Hook(alternateEquipmentIndexGetter, new hook_Inventory_get_alternateEquipmentIndex(Inventory_get_alternateEquipmentIndex));
            }
            else
            {
                Log.Error("Failed to find Inventory.alternateEquipmentIndex getter method");
            }

            On.RoR2.Inventory.GetActiveEquipment += Inventory_GetActiveEquipment;

            // exclude quality
            IL.RoR2.CharacterMaster.TrueKill_GameObject_GameObject_DamageTypeCombo += CharacterMaster_TrueKill_GameObject_GameObject_DamageTypeCombo;

            // include quality
            IL.RoR2.HealthComponent.ProcParry += genericPatchAllGetEquipmentQuality;

            IL.EntityStates.RoboBallBoss.Weapon.DeployMinions.SummonMinion += genericPatchAllGetEquipmentQuality;

            IL.RoR2.Stats.StatManager.ProcessCharacterUpdateEvents += genericPatchAllGetEquipmentQuality;

            IL.RoR2.CraftableCatalog.IngredientSlotEntry.Validate_Inventory += genericPatchAllGetEquipmentQuality;

            IL.RoR2.CraftingController.GetGeneratedOptionsFromInteractor += genericPatchAllGetEquipmentQuality;

            IL.RoR2.EquipmentSlot.Execute += genericPatchAllGetEquipmentQuality;

            IL.RoR2.EquipmentSlot.OnEquipmentExecuted += genericPatchAllGetEquipmentQuality;

            IL.RoR2.GlobalEventManager.OnCharacterDeath += genericPatchAllGetEquipmentQuality;

            IL.RoR2.CharacterBody.OnInventoryChanged += CharacterBody_OnInventoryChanged;

            MethodInfo summonDetachableMethod = typeof(EntityStates.SolusAmalgamator.DetatchState).GetMethod(nameof(EntityStates.SolusAmalgamator.DetatchState.SummonDetachable), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (summonDetachableMethod != null)
            {
                using DynamicMethodDefinition dmd = new DynamicMethodDefinition(summonDetachableMethod);
                using ILContext il = new ILContext(dmd.Definition);

                ILCursor c = new ILCursor(il);

                MethodReference onSpawnedServerMethodRef = null;
                if (c.TryGotoNext(x => x.MatchLdftn(out onSpawnedServerMethodRef),
                                  x => x.MatchNewobj<Action<SpawnCard.SpawnResult>>(),
                                  x => x.MatchStfld<DirectorSpawnRequest>(nameof(DirectorSpawnRequest.onSpawnedServer))))
                {
                    MethodBase onSpawnedServerMethod = null;
                    try
                    {
                        onSpawnedServerMethod = onSpawnedServerMethodRef.ResolveReflection();
                    }
                    catch (Exception e)
                    {
                        Log.Error_NoCallerPrefix($"Failed to resolve EntityStates.SolusAmalgamator.DetatchState.SummonDetachable onSpawnedServer method: {e}");
                    }

                    if (onSpawnedServerMethod != null)
                    {
                        new ILHook(onSpawnedServerMethod, genericPatchAllGetEquipmentQuality);
                    }
                }
                else
                {
                    Log.Error("Failed to find onSpawnedServer method in EntityStates.SolusAmalgamator.DetatchState.SummonDetachable");
                }
            }
            else
            {
                Log.Error("Failed to find method EntityStates.SolusAmalgamator.DetatchState.SummonDetachable");
            }

            MethodInfo spawnMineMethod = typeof(EntityStates.MinePod.MinePlant).GetMethod(nameof(EntityStates.MinePod.MinePlant.SpawnMine), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (spawnMineMethod != null)
            {
                using DynamicMethodDefinition dmd = new DynamicMethodDefinition(spawnMineMethod);
                using ILContext il = new ILContext(dmd.Definition);

                ILCursor c = new ILCursor(il);

                MethodReference onSpawnedServerMethodRef = null;
                if (c.TryGotoNext(x => x.MatchLdftn(out onSpawnedServerMethodRef),
                                  x => x.MatchNewobj<Action<SpawnCard.SpawnResult>>(),
                                  x => x.MatchStfld<DirectorSpawnRequest>(nameof(DirectorSpawnRequest.onSpawnedServer))))
                {
                    MethodBase onSpawnedServerMethod = null;
                    try
                    {
                        onSpawnedServerMethod = onSpawnedServerMethodRef.ResolveReflection();
                    }
                    catch (Exception e)
                    {
                        Log.Error_NoCallerPrefix($"Failed to resolve EntityStates.MinePod.MinePlant.SpawnMine onSpawnedServer method: {e}");
                    }

                    if (onSpawnedServerMethod != null)
                    {
                        new ILHook(onSpawnedServerMethod, genericPatchAllGetEquipmentQuality);
                    }
                }
                else
                {
                    Log.Error("Failed to find onSpawnedServer method in EntityStates.MinePod.MinePlant.SpawnMine");
                }
            }
            else
            {
                Log.Error("Failed to find method EntityStates.MinePod.MinePlant.SpawnMine");
            }

            MethodInfo equipmentPayCostMethod = CostTypeCatalog.GetCostTypeDef(CostTypeIndex.Equipment)?.payCost?.Method;
            if (equipmentPayCostMethod != null)
            {
                new ILHook(equipmentPayCostMethod, genericPatchAllGetEquipmentQuality);
            }
            else
            {
                Log.Error("Failed to find Equipment payCost method");
            }

            IL.RoR2.UI.LogBook.LogBookController.CanSelectEquipmentEntry += qualityEquipmentCanDropPatch;

            MethodInfo gameCompletionStatsAddPickupMethod = typeof(GameCompletionStatsHelper).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).SingleOrDefault(m => m.Name.StartsWith("<.ctor>g__AddPickup|"));
            if (gameCompletionStatsAddPickupMethod != null)
            {
                new ILHook(gameCompletionStatsAddPickupMethod, qualityEquipmentCanDropPatch);
            }
            else
            {
                Log.Error("Failed to find GameCompletionStatsHelper..ctor AddPickup local function");
            }
        }

        static bool EquipmentSlot_PerformEquipmentAction(On.RoR2.EquipmentSlot.orig_PerformEquipmentAction orig, EquipmentSlot self, EquipmentDef equipmentDef)
        {
            QualityTier equipmentQualityTier = QualityTier.None;
            if (equipmentDef)
            {
                equipmentQualityTier = QualityCatalog.GetQualityTier(equipmentDef.equipmentIndex);

                EquipmentIndex baseQualityEquipmentIndex = QualityCatalog.GetEquipmentIndexOfQuality(equipmentDef.equipmentIndex, QualityTier.None);
                if (baseQualityEquipmentIndex != EquipmentIndex.None && equipmentDef.equipmentIndex != baseQualityEquipmentIndex)
                {
                    equipmentDef = EquipmentCatalog.GetEquipmentDef(baseQualityEquipmentIndex);
                }
            }

            if (!_equipmentActionsBySlot.TryGetValue(self, out List<EquipmentAction> actions))
            {
                _equipmentActionsBySlot.Add(self, actions = ListPool<EquipmentAction>.RentCollection());
            }

            actions.Add(new EquipmentAction(self, equipmentQualityTier));

            try
            {
                return orig(self, equipmentDef);
            }
            finally
            {
                if (actions.Count > 0)
                {
                    actions.RemoveAt(actions.Count - 1);
                }

                if (actions.Count == 0)
                {
                    ListPool<EquipmentAction>.ReturnCollection(actions);
                    _equipmentActionsBySlot.Remove(self);
                }
            }
        }

        static void CharacterModel_SetEquipmentDisplay(On.RoR2.CharacterModel.orig_SetEquipmentDisplay orig, CharacterModel self, EquipmentIndex newEquipmentIndex)
        {
            orig(self, QualityCatalog.GetEquipmentIndexOfQuality(newEquipmentIndex, QualityTier.None));
        }

        static void CharacterModel_HighlightEquipentDisplay(On.RoR2.CharacterModel.orig_HighlightEquipentDisplay orig, CharacterModel self, EquipmentIndex equipmentIndex)
        {
            orig(self, QualityCatalog.GetEquipmentIndexOfQuality(equipmentIndex, QualityTier.None));
        }

        delegate EquipmentIndex orig_Inventory_get_currentEquipmentIndex(Inventory self);
        delegate EquipmentIndex hook_Inventory_get_currentEquipmentIndex(orig_Inventory_get_currentEquipmentIndex orig, Inventory self);
        static EquipmentIndex Inventory_get_currentEquipmentIndex(orig_Inventory_get_currentEquipmentIndex orig, Inventory self)
        {
            return QualityCatalog.GetEquipmentIndexOfQuality(orig(self), QualityTier.None);
        }

        delegate EquipmentIndex orig_Inventory_get_alternateEquipmentIndex(Inventory self);
        delegate EquipmentIndex hook_Inventory_get_alternateEquipmentIndex(orig_Inventory_get_alternateEquipmentIndex orig, Inventory self);
        static EquipmentIndex Inventory_get_alternateEquipmentIndex(orig_Inventory_get_alternateEquipmentIndex orig, Inventory self)
        {
            return QualityCatalog.GetEquipmentIndexOfQuality(orig(self), QualityTier.None);
        }

        static EquipmentState Inventory_GetActiveEquipment(On.RoR2.Inventory.orig_GetActiveEquipment orig, Inventory self)
        {
            return orig(self).WithQualityTier(QualityTier.None);
        }

        static void CharacterMaster_TrueKill_GameObject_GameObject_DamageTypeCombo(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            ILLabel healAndReviveBlockEndLabel = null;
            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchLdsfld(typeof(DLC2Content.Equipment), nameof(DLC2Content.Equipment.HealAndRevive)),
                               x => x.MatchCallOrCallvirt<EquipmentDef>("get_" + nameof(EquipmentDef.equipmentIndex)),
                               x => x.MatchBneUn(out healAndReviveBlockEndLabel)))
            {
                Log.Error("Failed to find seed of life equipment check location");
                return;
            }

            if (!c.TryGotoPrev(MoveType.After,
                               x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetEquipment))))
            {
                Log.Error("Failed to find seed of life equipment state location");
                return;
            }

            VariableDefinition originalEquipmentStateVar = il.AddVariable<EquipmentState>();

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Stloc, originalEquipmentStateVar);

            int equipmentReferencePatchCount = 0;
            while (c.TryGotoNext(MoveType.After,
                                 x => x.MatchLdsfld(typeof(DLC2Content.Equipment), nameof(DLC2Content.Equipment.HealAndRevive)) || x.MatchLdsfld(typeof(DLC2Content.Equipment), nameof(DLC2Content.Equipment.HealAndReviveConsumed))))
            {
                c.Emit(OpCodes.Ldloc, originalEquipmentStateVar);
                c.EmitDelegate<Func<EquipmentDef, EquipmentState, EquipmentDef>>(getEquipmentOfQualityInState);

                static EquipmentDef getEquipmentOfQualityInState(EquipmentDef equipmentDef, EquipmentState equipmentState)
                {
                    EquipmentIndex equipmentIndex = equipmentDef ? equipmentDef.equipmentIndex : EquipmentIndex.None;
                    if (equipmentIndex != EquipmentIndex.None)
                    {
                        QualityTier qualityTier = QualityCatalog.GetQualityTier(equipmentState.equipmentIndex);
                        EquipmentIndex qualityEquipmentIndex = QualityCatalog.GetEquipmentIndexOfQuality(equipmentIndex, qualityTier);
                        if (qualityEquipmentIndex != equipmentIndex)
                        {
                            equipmentDef = EquipmentCatalog.GetEquipmentDef(qualityEquipmentIndex);
                            equipmentIndex = qualityEquipmentIndex;
                        }
                    }

                    return equipmentDef;
                }

                equipmentReferencePatchCount++;
            }

            if (equipmentReferencePatchCount == 0)
            {
                Log.Error("Failed to find quality seed of life patch location");
            }
            else
            {
                Log.Debug($"Found {equipmentReferencePatchCount} quality seed of life patch location(s)");
            }
        }

        static void CharacterBody_OnInventoryChanged(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition qualityTierTempVar = null;

            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchCallOrCallvirt<Inventory>("get_" + nameof(Inventory.currentEquipmentIndex))))
            {
                patchSingleEquipmentQuality(c, ref qualityTierTempVar);
            }
            else
            {
                Log.Error("Failed to find patch location");
            }
        }

        static void genericPatchAllGetEquipmentQuality(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            static bool matchGetEquipmentCall(Instruction x)
            {
                return x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetActiveEquipment)) ||
                       x.MatchCallOrCallvirt<Inventory>("get_" + nameof(Inventory.currentEquipmentIndex)) ||
                       x.MatchCallOrCallvirt<Inventory>("get_" + nameof(Inventory.alternateEquipmentIndex)) ||
                       x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetEquipmentIndex)) ||
                       x.MatchCallOrCallvirt<EquipmentSlot>("get_" + nameof(EquipmentSlot.equipmentIndex));
            }

            VariableDefinition qualityTierTempVar = null;

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.Before,
                                 matchGetEquipmentCall))
            {
                patchSingleEquipmentQuality(c, ref qualityTierTempVar);

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

        static void patchSingleEquipmentQuality(ILCursor cursor, ref VariableDefinition qualityTierTempVar)
        {
            ILCursor c = cursor.Clone();

            if (!c.Next.MatchCallOrCallvirt(out MethodReference method) ||
                method.Parameters.Count != 0 ||
                !method.HasThis ||
                (!method.DeclaringType.Is(typeof(Inventory)) && !method.DeclaringType.Is(typeof(EquipmentSlot))) ||
                (!method.ReturnType.Is(typeof(EquipmentState)) && !method.ReturnType.Is(typeof(EquipmentIndex))))
            {
                Log.Error($"{c.Method.FullName}:{c.Index:X4} Cursor must be placed before a call that gets an EquipmentIndex/State from an Inventory/EquipmentSlot instance");
                return;
            }

            c.Emit(OpCodes.Dup);

            if (method.DeclaringType.Is(typeof(EquipmentSlot)))
            {
                c.EmitDelegate<Func<EquipmentSlot, QualityTier>>(EquipmentExtensions.GetActiveEquipmentQualityTier);
            }
            else // method.DeclaringType.Is(typeof(Inventory))
            {
                c.EmitDelegate<Func<Inventory, QualityTier>>(EquipmentExtensions.GetActiveEquipmentQualityTier);
            }

            qualityTierTempVar ??= c.Context.AddVariable<QualityTier>();
            c.Emit(OpCodes.Stloc, qualityTierTempVar);

            c.Index++;

            c.Emit(OpCodes.Ldloc, qualityTierTempVar);

            if (method.ReturnType.Is(typeof(EquipmentState)))
            {
                c.EmitDelegate<Func<EquipmentState, QualityTier, EquipmentState>>(EquipmentExtensions.WithQualityTier);
            }
            else // method.ReturnType.Is(typeof(EquipmentIndex))
            {
                c.EmitDelegate<Func<EquipmentIndex, QualityTier, EquipmentIndex>>(QualityCatalog.GetEquipmentIndexOfQuality);
            }
        }

        static void qualityEquipmentCanDropPatch(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchLdfld<EquipmentDef>(nameof(EquipmentDef.canDrop))))
            {
                c.EmitDelegate<Func<EquipmentDef, EquipmentDef>>(getBaseEquipment);

                static EquipmentDef getBaseEquipment(EquipmentDef equipmentDef)
                {
                    EquipmentIndex equipmentIndex = equipmentDef ? equipmentDef.equipmentIndex : EquipmentIndex.None;
                    if (equipmentIndex != EquipmentIndex.None)
                    {
                        EquipmentIndex baseEquipmentIndex = QualityCatalog.GetEquipmentIndexOfQuality(equipmentIndex, QualityTier.None);
                        if (baseEquipmentIndex != equipmentIndex)
                        {
                            equipmentDef = EquipmentCatalog.GetEquipmentDef(baseEquipmentIndex);
                            equipmentIndex = baseEquipmentIndex;
                        }
                    }

                    return equipmentDef;
                }

                patchCount++;
                c.SearchTarget = SearchTarget.Next;
            }

            if (patchCount == 0)
            {
                Log.Error($"{il.Method.FullName} Failed to find patch location");
            }
            else
            {
                Log.Debug($"{il.Method.FullName} Found {patchCount} patch location(s)");
            }
        }
    }
}
