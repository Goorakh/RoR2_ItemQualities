using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Items;
using System;
using UnityEngine;

namespace ItemQualities
{
    static class DamageTypes
    {
        public static DamageAPI.ModdedDamageType Frost6s { get; private set; }

        public static DamageAPI.ModdedDamageType ProcOnly { get; private set; }

        public static DamageAPI.ModdedDamageType ForceAddToSharedSuffering { get; private set; }

        [SystemInitializer]
        static void Init()
        {
            Frost6s = DamageAPI.ReserveDamageType();
            ProcOnly = DamageAPI.ReserveDamageType();
            ForceAddToSharedSuffering = DamageAPI.ReserveDamageType();

            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;

            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess_ProcOnlyPatch;
        }

        static void onServerDamageDealt(DamageReport damageReport)
        {
            if (damageReport?.damageInfo == null)
                return;

            DamageInfo damageInfo = damageReport.damageInfo;

            GameObject attacker = damageReport.attacker;

            CharacterBody victimBody = damageReport.victimBody;
            HealthComponent victimHealthComponent = damageReport.victim;

            if (victimHealthComponent && victimBody)
            {
                if (damageInfo.damageType.HasModdedDamageType(Frost6s))
                {
                    if (!victimHealthComponent.isInFrozenState && !victimBody.HasBuff(DLC2Content.Buffs.FreezeImmune))
                    {
                        victimBody.AddTimedBuff(DLC2Content.Buffs.Frost, 6f, 6);
                    }
                }

                if (damageInfo.damageType.HasModdedDamageType(ForceAddToSharedSuffering))
                {
                    if (victimBody.teamComponent.teamIndex != TeamIndex.None && !victimBody.HasBuff(DLC3Content.Buffs.SharedSuffering))
                    {
                        if (attacker && attacker.TryGetComponent(out SharedSufferingItemBehaviour sharedSufferingItemBehaviour))
                        {
                            victimBody.AddBuff(DLC3Content.Buffs.SharedSuffering);
                            if (!sharedSufferingItemBehaviour.afflicted.Contains(victimBody))
                            {
                                sharedSufferingItemBehaviour.afflicted.Add(victimBody);
                                sharedSufferingItemBehaviour.afflictedDirty = true;
                            }
                        }
                    }
                }
            }
        }

        static void HealthComponent_TakeDamageProcess_ProcOnlyPatch(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.PermanentCurse)),
                               x => x.MatchCallOrCallvirt<HealthComponent>(nameof(HealthComponent.TakeDamageForce))))
            {
                Log.Error("Failed to find patch end location");
                return;
            }

            c.Goto(foundCursors[0].Next, MoveType.Before); // ldsfld RoR2Content.Buffs.PermanentCurse

            ILLabel startHurtBlockLabel = default;
            if (!c.TryGotoPrev(x => x.MatchLdfld<DamageInfo>(nameof(DamageInfo.delayedDamageSecondHalf))) ||
                !c.TryGotoNext(x => x.MatchBrtrue(out startHurtBlockLabel)))
            {
                Log.Error("Failed to find patch start location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After); // call HealthComponent.TakeDamageForce
            c.MoveAfterLabels();
            ILLabel endHurtBlockLabel = c.MarkLabel();

            c.Goto(startHurtBlockLabel.Target, MoveType.AfterLabel);

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Func<DamageInfo, bool>>(shouldDealDamage);
            c.Emit(OpCodes.Brfalse, endHurtBlockLabel);

            static bool shouldDealDamage(DamageInfo damageInfo)
            {
                return damageInfo == null || !damageInfo.HasModdedDamageType(ProcOnly);
            }
        }
    }
}
