using EntityStates;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using System.Runtime.CompilerServices;

namespace ItemQualities.Items
{
    static class SecondarySkillMagazine
    {
        /// <summary>
        /// For skills such as loader grapple that deduct stock manually rather than immediately on skill use
        /// </summary>
        public static event Action<GenericSkill> OnSkillUsedIndirectAuthority;

        [SystemInitializer]
        static void Init()
        {
            IL.EntityStates.Railgunner.Weapon.BaseFireSnipe.OnEnter += BaseFireSnipe_OnEnter;
            IL.RoR2.Projectile.ProjectileGrappleController.FlyState.DeductOwnerStock += FlyState_DeductOwnerStock;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void invokeOnSkillUsedIndirectAuthority(GenericSkill skill)
        {
            if (skill)
            {
                OnSkillUsedIndirectAuthority?.Invoke(skill);
            }
        }

        static void emitGetEntityStateSecondarySkill(ILCursor c)
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<EntityState, GenericSkill>>(entityStateGetSecondarySkill);
        }

        static GenericSkill entityStateGetSecondarySkill(EntityState entityState)
        {
            if (entityState.skillLocator)
            {
                return entityState.skillLocator.secondary;
            }

            return null;
        }

        static void emitOnSkillUsedIndirectAuthority(ILCursor c)
        {
            c.EmitDelegate<Action<GenericSkill>>(invokeOnSkillUsedIndirectAuthority);
        }

        static void BaseFireSnipe_OnEnter(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<GenericSkill>(nameof(GenericSkill.DeductStock))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            emitGetEntityStateSecondarySkill(c);
            emitOnSkillUsedIndirectAuthority(c);
        }

        static void FlyState_DeductOwnerStock(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition secondarySkillVar = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdloc<GenericSkill>(il, out secondarySkillVar),
                               x => x.MatchLdcI4(out _),
                               x => x.MatchCallOrCallvirt<GenericSkill>(nameof(GenericSkill.DeductStock))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldloc, secondarySkillVar);
            emitOnSkillUsedIndirectAuthority(c);
        }
    }
}
