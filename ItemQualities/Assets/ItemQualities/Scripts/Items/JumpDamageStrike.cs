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
        }

        static void JumpDamageStrikeBodyBehavior_DischargeEffects(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchStfld<BlastAttack>(nameof(BlastAttack.baseDamage))))
            {
                Log.Error("Failed to find damage patch location");
            }
            else
            {
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

            c.Goto(0);

            if (!c.TryGotoNext(MoveType.Before,
                               x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.SetBuffCount))))
            {
                Log.Error("Failed to find charge decrease patch location");
            }
            else
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<int, JumpDamageStrikeBodyBehavior, int>>(getBuffCountAfterDischarge);

                static int getBuffCountAfterDischarge(int newBuffCount, JumpDamageStrikeBodyBehavior jumpDamageStrikeBodyBehavior)
                {
                    CharacterBody body = jumpDamageStrikeBodyBehavior ? jumpDamageStrikeBodyBehavior.body : null;
                    if (body && body.inventory)
                    {
                        ItemQualityCounts jumpDamageStrike = body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.JumpDamageStrike);

                        int maxChargeToConsume;
                        switch (jumpDamageStrike.HighestQuality)
                        {
                            case QualityTier.None:
                                maxChargeToConsume = int.MaxValue;
                                break;
                            case QualityTier.Uncommon:
                                maxChargeToConsume = 75;
                                break;
                            case QualityTier.Rare:
                                maxChargeToConsume = 50;
                                break;
                            case QualityTier.Epic:
                                maxChargeToConsume = 25;
                                break;
                            case QualityTier.Legendary:
                                maxChargeToConsume = 10;
                                break;
                            default:
                                Log.Error($"Quality tier {jumpDamageStrike.HighestQuality} is not implemented");
                                maxChargeToConsume = int.MaxValue;
                                break;
                        }

                        int currentBuffCount = body.GetBuffCount(DLC3Content.Buffs.JumpDamageStrikeCharge);
                        if (currentBuffCount > maxChargeToConsume)
                        {
                            newBuffCount = currentBuffCount - maxChargeToConsume;
                        }
                    }

                    return newBuffCount;
                }
            }
        }
    }
}
