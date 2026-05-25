using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities
{
    internal static class TemporaryHealth
    {
        public static float GetTemporaryHealthBonus(this CharacterBody body)
        {
            return body.GetBuffCount(ItemQualitiesContent.Buffs.SlugHealth) +
                   body.GetBuffCount(ItemQualitiesContent.Buffs.FruitTempHealth);
        }

        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;

            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            VariableDefinition healthDamageDealtVar = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdarg(0),
                               x => x.MatchLdarg(0),
                               x => x.MatchLdfld<HealthComponent>(nameof(HealthComponent.shield)),
                               x => x.MatchLdloc(typeof(float), il, out healthDamageDealtVar),
                               x => x.MatchSub(),
                               x => x.MatchCallOrCallvirt<HealthComponent>("set_" + nameof(HealthComponent.Networkshield))))
            {
                Log.Error("Failed to find damageDealt variable index");
                return;
            }

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchCallOrCallvirt<GlobalEventManager>(nameof(GlobalEventManager.ServerDamageDealt))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, healthDamageDealtVar);
            c.EmitDelegate<Action<HealthComponent, float>>(onDamageDealt);

            static void onDamageDealt(HealthComponent healthComponent, float healthDamageDealt)
            {
                if (!healthComponent || !healthComponent.body)
                    return;

                if (healthDamageDealt > 0f)
                {
                    float temporaryHealthDamage = healthDamageDealt;

                    tryDeductTemporaryHealth(ref temporaryHealthDamage, ItemQualitiesContent.Buffs.SlugHealth);
                    tryDeductTemporaryHealth(ref temporaryHealthDamage, ItemQualitiesContent.Buffs.FruitTempHealth);

                    void tryDeductTemporaryHealth(ref float temporaryHealthDamage, BuffDef tempHealthBuff)
                    {
                        if (temporaryHealthDamage <= 0f)
                            return;

                        int tempHealthCount = healthComponent.body.GetBuffCount(tempHealthBuff);
                        if (tempHealthCount == 0)
                            return;

                        int tempHealthTaken = Mathf.FloorToInt(Math.Min(temporaryHealthDamage, tempHealthCount));
                        temporaryHealthDamage -= tempHealthTaken;

                        if (tempHealthTaken > 0)
                        {
                            healthComponent.body.SetBuffCount(tempHealthBuff.buffIndex, Mathf.Max(0, tempHealthCount - tempHealthTaken));
                        }
                    }
                }
            }
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            args.baseHealthAdd += sender.GetTemporaryHealthBonus();
        }
    }
}
