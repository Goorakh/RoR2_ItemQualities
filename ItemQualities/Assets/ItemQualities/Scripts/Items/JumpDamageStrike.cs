using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Items;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class JumpDamageStrike
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.Items.JumpDamageStrikeBodyBehavior.DischargeEffects += JumpDamageStrikeBodyBehavior_DischargeEffects;

            IL.RoR2.Items.JumpDamageStrikeBodyBehavior.UpdateCharge += JumpDamageStrikeBodyBehavior_UpdateCharge;
        }

        static void JumpDamageStrikeBodyBehavior_DischargeEffects(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchStfld<BlastAttack>(nameof(BlastAttack.baseDamage))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, JumpDamageStrikeBodyBehavior, float>>(getBlastDamage);

            static float getBlastDamage(float blastDamage, JumpDamageStrikeBodyBehavior jumpDamageStrikeBodyBehavior)
            {
                CharacterBody body = jumpDamageStrikeBodyBehavior ? jumpDamageStrikeBodyBehavior.body : null;
                if (body && body.inventory)
                {
                    ItemQualityCounts jumpDamageStrike = body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.JumpDamageStrike);
                    if (jumpDamageStrike.TotalQualityCount > 0)
                    {
                        float damageCoefficientPerMoveSpeedIncreaseCoefficient = (1.5f * jumpDamageStrike.UncommonCount) +
                                                                                 (2.5f * jumpDamageStrike.RareCount) +
                                                                                 (3.5f * jumpDamageStrike.EpicCount) +
                                                                                 (5.0f * jumpDamageStrike.LegendaryCount);

                        float currentMoveSpeedIncreaseCoefficient = body.baseMoveSpeed > 0 ? Mathf.Max(0f, (body.moveSpeed / body.baseMoveSpeed) - 1f) : 0f;

                        float damageCoefficient = damageCoefficientPerMoveSpeedIncreaseCoefficient * currentMoveSpeedIncreaseCoefficient;
                        blastDamage += damageCoefficient * body.damage;
                    }
                }

                return blastDamage;
            }
        }

        static void JumpDamageStrikeBodyBehavior_UpdateCharge(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld<JumpDamageStrikeBodyBehavior>(nameof(JumpDamageStrikeBodyBehavior.minDistancePerCharge)),
                               x => x.MatchLdsfld<JumpDamageStrikeBodyBehavior>(nameof(JumpDamageStrikeBodyBehavior.maxDistancePerCharge)),
                               x => x.MatchCallOrCallvirt<Mathf>(nameof(Mathf.Lerp))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.After);

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, JumpDamageStrikeBodyBehavior, float>>(getDistancePerCharge);

            static float getDistancePerCharge(float baseDistancePerCharge, JumpDamageStrikeBodyBehavior jumpDamageStrikeBodyBehavior)
            {
                float distancePerCharge = baseDistancePerCharge;

                if (jumpDamageStrikeBodyBehavior && jumpDamageStrikeBodyBehavior.body && jumpDamageStrikeBodyBehavior.body.inventory)
                {
                    ItemQualityCounts jumpDamageStrike = jumpDamageStrikeBodyBehavior.body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.JumpDamageStrike);

                    float chargeSpeed;
                    switch (jumpDamageStrike.HighestQuality)
                    {
                        case QualityTier.None:
                            chargeSpeed = 1f;
                            break;
                        case QualityTier.Uncommon:
                            chargeSpeed = 1.25f;
                            break;
                        case QualityTier.Rare:
                            chargeSpeed = 1.50f;
                            break;
                        case QualityTier.Epic:
                            chargeSpeed = 1.75f;
                            break;
                        case QualityTier.Legendary:
                            chargeSpeed = 2f;
                            break;
                        default:
                            chargeSpeed = 1f;
                            Log.Error($"Quality tier {jumpDamageStrike.HighestQuality} is not implemented");
                            break;
                    }

                    if (chargeSpeed > 1f)
                    {
                        distancePerCharge /= chargeSpeed;
                    }
                }

                return distancePerCharge;
            }
        }
    }
}
