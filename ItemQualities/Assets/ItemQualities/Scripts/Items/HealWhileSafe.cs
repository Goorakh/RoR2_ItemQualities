using ItemQualities.Orbs;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Orbs;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class HealWhileSafe
    {
        [SystemInitializer]
        static void Init()
        {
            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;

            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;

            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        static void onCharacterDeathGlobal(DamageReport damageReport)
        {
            if (!NetworkServer.active || damageReport?.damageInfo == null)
                return;

            if (!damageReport.attackerBody || !damageReport.attackerBody.outOfDanger || !damageReport.attackerBody.inventory)
                return;

            ItemQualityCounts slug = damageReport.attackerBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.HealWhileSafe);
            if (slug.TotalQualityCount == 0)
                return;

            int healthPerKill = (2 * slug.UncommonCount) +
                                (4 * slug.RareCount) +
                                (8 * slug.EpicCount) +
                                (12 * slug.LegendaryCount);

            int maxHealthIncrease = healthPerKill * 50;

            int currentHealthIncrease = damageReport.attackerBody.GetBuffCount(ItemQualitiesContent.Buffs.SlugHealth);

            int healthToAdd = Mathf.Min(healthPerKill, maxHealthIncrease - currentHealthIncrease);
            if (healthToAdd > 0)
            {
                SlugOrb slugOrb = new SlugOrb
                {
                    target = damageReport.attackerBody.mainHurtBox,
                    origin = damageReport.victimBody ? damageReport.victimBody.corePosition : damageReport.damageInfo.position,
                    SlugBuffCount = healthToAdd
                };

                OrbManager.instance.AddOrb(slugOrb);
            }
        }

        static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int healthDamageDealtVarIndex = -1;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdarg(0),
                               x => x.MatchLdarg(0),
                               x => x.MatchLdfld<HealthComponent>(nameof(HealthComponent.shield)),
                               x => x.MatchLdloc(typeof(float), il, out healthDamageDealtVarIndex),
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
            c.Emit(OpCodes.Ldloc, healthDamageDealtVarIndex);
            c.EmitDelegate<Action<HealthComponent, float>>(onDamageDealt);

            static void onDamageDealt(HealthComponent healthComponent, float healthDamageDealt)
            {
                if (!healthComponent || !healthComponent.body)
                    return;

                if (healthDamageDealt > 0f)
                {
                    if (healthComponent.body.HasBuff(ItemQualitiesContent.Buffs.SlugHealth))
                    {
                        healthComponent.body.SetBuffCount(ItemQualitiesContent.Buffs.SlugHealth.buffIndex, Mathf.Max(0, (int)(healthComponent.body.GetBuffCount(ItemQualitiesContent.Buffs.SlugHealth) - healthDamageDealt)));
                    }
                }
            }
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            args.baseHealthAdd += sender.GetBuffCount(ItemQualitiesContent.Buffs.SlugHealth);
        }
    }
}
