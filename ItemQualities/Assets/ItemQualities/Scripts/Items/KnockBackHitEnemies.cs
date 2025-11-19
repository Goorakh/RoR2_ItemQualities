using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class KnockBackHitEnemies
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.KnockbackFinUtil.ModifyDamageInfo += KnockbackFinUtil_ModifyDamageInfo;
        }

        static void KnockbackFinUtil_ModifyDamageInfo(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!il.Method.TryFindParameter<CharacterBody>("attacker", out ParameterDefinition attackerBodyParameter))
            {
                Log.Error("Failed to find attacker body parameter");
                return;
            }

            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdcR4(0.1f)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Emit(OpCodes.Ldarg, attackerBodyParameter);
            c.EmitDelegate<Func<float, CharacterBody, float>>(getDamageCoefficientPerBounce);

            static float getDamageCoefficientPerBounce(float damageCoefficient, CharacterBody attackerBody)
            {
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                ItemQualityCounts knockBackHitEnemies = ItemQualitiesContent.ItemQualityGroups.KnockBackHitEnemies.GetItemCountsEffective(attackerInventory);

                if (knockBackHitEnemies.TotalQualityCount > 0)
                {
                    damageCoefficient += (0.05f * knockBackHitEnemies.UncommonCount) +
                                         (0.10f * knockBackHitEnemies.RareCount) +
                                         (0.20f * knockBackHitEnemies.EpicCount) +
                                         (0.35f * knockBackHitEnemies.LegendaryCount);
                }

                return damageCoefficient;
            }
        }
    }
}
