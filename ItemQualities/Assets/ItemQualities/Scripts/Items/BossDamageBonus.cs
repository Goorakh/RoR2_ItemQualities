using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class BossDamageBonus
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
        }

        private static void onCharacterDeathGlobal(DamageReport report)
        {
            if(report.victimBody.HasBuff(ItemQualitiesContent.Buffs.MiniBossMarker)) 
            {
                ItemQualityCounts bossDamageBonus = ItemQualitiesContent.ItemQualityGroups.BossDamageBonus.GetItemCountsEffective(report.attackerBody.inventory);

                int maxHitlistBonus = bossDamageBonus.UncommonCount * 15 +
                                    bossDamageBonus.RareCount * 30 +
                                    bossDamageBonus.EpicCount * 45 +
                                    bossDamageBonus.LegendaryCount * 60;
                
                if(report.attackerBody.GetBuffCount(ItemQualitiesContent.Buffs.HitlistDamage) < maxHitlistBonus) 
                {
                    report.attackerBody.AddBuff(ItemQualitiesContent.Buffs.HitlistDamage);
                }
            }
        }

        static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.BossDamageBonus)),
                               x => x.MatchLdcR4(0.2f),
                               x => x.MatchMul()))
            {
                Log.Error("Failed to find damage patch location");
                return;
            }

            c.Goto(foundCursors[0].Next, MoveType.Before); // ldsfld RoR2Content.Items.BossDamageBonus
            if (!c.TryGotoPrev(MoveType.After,
                               x => x.MatchCallOrCallvirt<CharacterBody>("get_" + nameof(CharacterBody.isBoss))))
            {
                Log.Error("Failed to find isBoss patch location");
                return;
            }

            VariableDefinition isMiniBossVar = il.AddVariable<bool>();

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Func<bool, HealthComponent, DamageInfo, bool>>(isMiniBoss);
            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Stloc, isMiniBossVar);
            c.Emit(OpCodes.Or);

            static bool isMiniBoss(bool isBoss, HealthComponent victim, DamageInfo damageInfo)
            {
                if (isBoss)
                    return false;

                if (!victim || !victim.body)
                    return false;

                CharacterBody attackerBody = damageInfo?.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                ItemQualityCounts bossDamageBonus = ItemQualitiesContent.ItemQualityGroups.BossDamageBonus.GetItemCountsEffective(attackerInventory);
                return bossDamageBonus.TotalQualityCount > 0 && victim.body.HasBuff(ItemQualitiesContent.Buffs.MiniBossMarker);
            }

            c.Goto(foundCursors[2].Next, MoveType.After); // mul

            c.Emit(OpCodes.Ldloc, isMiniBossVar);
            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Func<float, bool, DamageInfo, float>>(getBossDamageMultiplier);

            static float getBossDamageMultiplier(float damageMultiplier, bool isMiniBoss, DamageInfo damageInfo)
            {
                if (isMiniBoss)
                {
                    CharacterBody attackerBody = damageInfo?.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                    damageMultiplier = attackerBody.GetBuffCount(ItemQualitiesContent.Buffs.HitlistDamage) * 0.01f;
                }

                return damageMultiplier;
            }
        }
    }
}
