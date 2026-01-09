using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities
{
    static class AttackCollisionHooks
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.BulletAttack.DefaultFilterCallbackImplementation += BulletAttack_DefaultFilterCallbackImplementation;
            On.RoR2.OverlapAttack.HurtBoxPassesFilter += OverlapAttack_HurtBoxPassesFilter;
            IL.RoR2.BlastAttack.CollectHits += BlastAttack_CollectHits;
        }

        static bool BulletAttack_DefaultFilterCallbackImplementation(On.RoR2.BulletAttack.orig_DefaultFilterCallbackImplementation orig, BulletAttack bulletAttack, ref BulletAttack.BulletHit hitInfo)
        {
            if (!orig(bulletAttack, ref hitInfo))
                return false;

            try
            {
                if (bulletAttack.owner && bulletAttack.owner.TryGetComponentCached(out ObjectCollisionManager ownerCollisionManager))
                {
                    if (ownerCollisionManager.IgnoresCollisionsWith(hitInfo.collider))
                        return false;
                }
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e);
            }

            return true;
        }

        static bool OverlapAttack_HurtBoxPassesFilter(On.RoR2.OverlapAttack.orig_HurtBoxPassesFilter orig, OverlapAttack self, HurtBox hurtBox)
        {
            if (!orig(self, hurtBox))
                return false;

            try
            {
                if (hurtBox && self.attacker && self.attacker.TryGetComponentCached(out ObjectCollisionManager attackerCollisionManager))
                {
                    if (attackerCollisionManager.IgnoresCollisionsWith(hurtBox.collider))
                        return false;
                }
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e);
            }

            return true;
        }

        static void BlastAttack_CollectHits(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int hurtBoxVarIndex = -1;
            ILLabel hurtBoxInvalidLabel = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdloc(typeof(HurtBox), il, out hurtBoxVarIndex),
                               x => x.MatchImplicitConversion<UnityEngine.Object, bool>(),
                               x => x.MatchBrfalse(out hurtBoxInvalidLabel)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, hurtBoxVarIndex);
            c.EmitDelegate<Func<BlastAttack, HurtBox, bool>>(attackerIgnoresCollisionWith);
            c.Emit(OpCodes.Brtrue, hurtBoxInvalidLabel);

            static bool attackerIgnoresCollisionWith(BlastAttack blastAttack, HurtBox hurtBox)
            {
                if (hurtBox &&
                    hurtBox.collider &&
                    blastAttack != null &&
                    blastAttack.attacker &&
                    blastAttack.attacker.TryGetComponentCached(out ObjectCollisionManager attackerCollisionManager) &&
                    attackerCollisionManager.IgnoresCollisionsWith(hurtBox.collider))
                {
                    return true;
                }

                return false;
            }
        }
    }
}
