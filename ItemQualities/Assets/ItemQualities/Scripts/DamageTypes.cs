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

        public static DamageAPI.ModdedDamageType ForceAddToSharedSuffering { get; private set; }

        public static DamageAPI.ModdedDamageType DontDoItemDropsPrettyPlease { get; private set; }

        [SystemInitializer]
        static void Init()
        {
            Frost6s = DamageAPI.ReserveDamageType();
            ForceAddToSharedSuffering = DamageAPI.ReserveDamageType();
            DontDoItemDropsPrettyPlease = DamageAPI.ReserveDamageType();

            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;

            IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
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

        static void GlobalEventManager_OnCharacterDeath(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageReport>(out ParameterDefinition damageReportParameter))
            {
                Log.Error("Failed to find DamageReport parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            ILLabel afterSonorousLabel = null;
            if (c.TryGotoNext(MoveType.AfterLabel,
                              x => x.MatchLdloc(out _), // attackerMaster
                              x => x.MatchCallOrCallvirt<CharacterMaster>("get_" + nameof(CharacterMaster.inventory)),
                              x => x.MatchLdsfld(typeof(DLC2Content.Items), nameof(DLC2Content.Items.ItemDropChanceOnKill)),
                              x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCountEffective)),
                              x => x.MatchLdcI4(0),
                              x => x.MatchBle(out afterSonorousLabel)))
            {
                c.Emit(OpCodes.Ldarg, damageReportParameter);
                c.EmitDelegate<Func<DamageReport, bool>>(allowSonorousDrop);
                c.Emit(OpCodes.Brfalse, afterSonorousLabel);

                static bool allowSonorousDrop(DamageReport damageReport)
                {
                    return !damageReport.damageInfo.damageType.HasModdedDamageType(DontDoItemDropsPrettyPlease);
                }
            }
            else
            {
                Log.Error("Failed to find sonorous disable patch location");
            }
        }
    }
}
