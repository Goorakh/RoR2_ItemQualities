using EntityStates;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using RoR2;
using System;
using System.Runtime.CompilerServices;

namespace ItemQualities
{
    static class SkillHooks
    {
        /// <summary>
        /// For skills such as loader grapple that deduct stock manually rather than immediately on skill use
        /// </summary>
        public static event Action<GenericSkill> OnSkillUsedIndirectAuthority;

        [SystemInitializer]
        static void Init()
        {
            EntityStatePatcher.AddPatcher(new EntityStatePatcher.PatcherInfo
            {
                Manipulator = entityStateStockDeductHookManipulator,
                ShouldApplyPredicate = matchEntityStateDeductStocks,
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void invokeOnSkillUsedIndirectAuthority(GenericSkill skill)
        {
            OnSkillUsedIndirectAuthority?.Invoke(skill);
        }

        static void emitOnSkillUsedIndirectAuthority(ILCursor c)
        {
            c.EmitDelegate<Action<GenericSkill>>(invokeOnSkillUsedIndirectAuthority);
        }

        static bool matchEntityStateDeductStocks(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchLdarg(0),
                              x => x.MatchCallOrCallvirt<BaseSkillState>("get_" + nameof(BaseSkillState.activatorSkillSlot)),
                              x => x.MatchLdcI4(out _),
                              x => x.MatchCallOrCallvirt<GenericSkill>(nameof(GenericSkill.DeductStock))))
            {
                return true;
            }

            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchLdarg(0),
                              x => x.MatchCallOrCallvirt<EntityState>("get_" + nameof(EntityState.skillLocator)),
                              x => x.MatchLdfld(out _), // skill
                              x => x.MatchLdcI4(out _),
                              x => x.MatchCallOrCallvirt<GenericSkill>(nameof(GenericSkill.DeductStock))))
            {
                return true;
            }

            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchLdloc<SkillLocator>(il, out _),
                              x => x.MatchLdfld(out _), // skill
                              x => x.MatchLdcI4(out _),
                              x => x.MatchCallOrCallvirt<GenericSkill>(nameof(GenericSkill.DeductStock))))
            {
                return true;
            }

            if (c.TryGotoNext(MoveType.Before,
                              x => x.MatchLdloc<GenericSkill>(il, out _),
                              x => x.MatchLdcI4(out _),
                              x => x.MatchCallOrCallvirt<GenericSkill>(nameof(GenericSkill.DeductStock))))
            {
                return true;
            }

            return false;
        }

        static void entityStateStockDeductHookManipulator(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            bool anyPatchSucceeded = false;

            // activatorSkillSlot
            {
                c.Goto(0);

                int patchCount = 0;

                MethodReference activatorSkillSlotGetterMethod = null;
                while (c.TryGotoNext(MoveType.After,
                                     x => x.MatchLdarg(0),
                                     x => x.MatchCallOrCallvirt(out activatorSkillSlotGetterMethod) && activatorSkillSlotGetterMethod.Is(typeof(BaseSkillState), "get_" + nameof(BaseSkillState.activatorSkillSlot)),
                                     x => x.MatchLdcI4(out _),
                                     x => x.MatchCallOrCallvirt<GenericSkill>(nameof(GenericSkill.DeductStock))))
                {
                    c.Emit(OpCodes.Ldarg_0);
                    c.Emit(OpCodes.Call, activatorSkillSlotGetterMethod);
                    emitOnSkillUsedIndirectAuthority(c);

                    patchCount++;
                }

                if (patchCount > 0)
                {
                    Log.Debug($"[{il.Method.FullName}] Found {patchCount} activatorSkillSlot patch location(s)");
                    anyPatchSucceeded = true;
                }
            }

            // skillLocator (property) -> field
            {
                c.Goto(0);

                int patchCount = 0;

                MethodReference skillLocatorGetterMethod = null;
                FieldReference skillField = null;
                while (c.TryGotoNext(MoveType.After,
                                     x => x.MatchLdarg(0),
                                     x => x.MatchCallOrCallvirt(out skillLocatorGetterMethod) && skillLocatorGetterMethod.Is(typeof(EntityState), "get_" + nameof(EntityState.skillLocator)),
                                     x => x.MatchLdfld(out skillField),
                                     x => x.MatchLdcI4(out _),
                                     x => x.MatchCallOrCallvirt<GenericSkill>(nameof(GenericSkill.DeductStock))))
                {
                    c.Emit(OpCodes.Ldarg_0);
                    c.Emit(OpCodes.Call, skillLocatorGetterMethod);
                    c.Emit(OpCodes.Ldfld, skillField);
                    emitOnSkillUsedIndirectAuthority(c);

                    patchCount++;
                }

                if (patchCount > 0)
                {
                    Log.Debug($"[{il.Method.FullName}] Found {patchCount} skillLocator (property) field patch location(s)");
                    anyPatchSucceeded = true;
                }
            }

            // skillLocator (local) -> field
            {
                c.Goto(0);

                int patchCount = 0;

                VariableDefinition skillLocatorVar = null;
                FieldReference skillField = null;
                while (c.TryGotoNext(MoveType.After,
                                     x => x.MatchLdloc<SkillLocator>(il, out skillLocatorVar),
                                     x => x.MatchLdfld(out skillField),
                                     x => x.MatchLdcI4(out _),
                                     x => x.MatchCallOrCallvirt<GenericSkill>(nameof(GenericSkill.DeductStock))))
                {
                    c.Emit(OpCodes.Ldloc, skillLocatorVar);
                    c.Emit(OpCodes.Ldfld, skillField);
                    emitOnSkillUsedIndirectAuthority(c);

                    patchCount++;
                }

                if (patchCount > 0)
                {
                    Log.Debug($"[{il.Method.FullName}] Found {patchCount} skillLocator (local) field patch location(s)");
                    anyPatchSucceeded = true;
                }
            }

            // skill (local)
            {
                c.Goto(0);

                int patchCount = 0;

                VariableDefinition skillVar = null;
                while (c.TryGotoNext(MoveType.After,
                                     x => x.MatchLdloc<GenericSkill>(il, out skillVar),
                                     x => x.MatchLdcI4(out _),
                                     x => x.MatchCallOrCallvirt<GenericSkill>(nameof(GenericSkill.DeductStock))))
                {
                    c.Emit(OpCodes.Ldloc, skillVar);
                    emitOnSkillUsedIndirectAuthority(c);

                    patchCount++;
                }

                if (patchCount > 0)
                {
                    Log.Debug($"[{il.Method.FullName}] Found {patchCount} skill (local) field patch location(s)");
                    anyPatchSucceeded = true;
                }
            }

            if (!anyPatchSucceeded)
            {
                Log.Warning($"[{il.Method.FullName}] Failed to find DeductStock patch location");
            }
        }
    }
}
