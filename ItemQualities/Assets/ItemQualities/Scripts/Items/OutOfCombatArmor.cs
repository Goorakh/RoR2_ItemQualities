using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class OutOfCombatArmor
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.CharacterBody.OnTakeDamageServer += CharacterBody_OnTakeDamageServer;
        }

        static void CharacterBody_OnTakeDamageServer(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageReport>(out ParameterDefinition damageReportParameter))
            {
                Log.Error("Failed to find DamageReport parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchStfld<CharacterBody>(nameof(CharacterBody.outOfDangerStopwatch))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, damageReportParameter);
            c.EmitDelegate<Action<CharacterBody, DamageReport>>(onEnterDanger);

            static void onEnterDanger(CharacterBody victim, DamageReport damageReport)
            {
                if (!victim || damageReport?.damageInfo == null)
                    return;

                if (!damageReport.attacker)
                    return;

                if (victim.HasBuff(DLC1Content.Buffs.OutOfCombatArmorBuff))
                {
                    ItemQualityCounts outOfCombatArmor = ItemQualitiesContent.ItemQualityGroups.OutOfCombatArmor.GetItemCounts(victim.inventory);
                    if (outOfCombatArmor.TotalQualityCount > 0)
                    {
                        float stunDuration = (1f * outOfCombatArmor.UncommonCount) +
                                             (2f * outOfCombatArmor.RareCount) +
                                             (4f * outOfCombatArmor.EpicCount) +
                                             (6f * outOfCombatArmor.LegendaryCount);

                        if (damageReport.attacker.TryGetComponent(out SetStateOnHurt attackerSetStateOnHurt) && attackerSetStateOnHurt.canBeStunned)
                        {
                            attackerSetStateOnHurt.SetStun(stunDuration);
                        }
                    }
                }
            }
        }
    }
}
