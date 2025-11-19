using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Orbs;
using System;

namespace ItemQualities.Items
{
    static class MissileVoid
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.ProcessHitEnemy += GlobalEventManager_ProcessHitEnemy;
        }

        static void GlobalEventManager_ProcessHitEnemy(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.MissileVoid)),
                               x => x.MatchNewobj<MissileVoidOrb>(),
                               x => x.MatchStfld<GenericDamageOrb>(nameof(GenericDamageOrb.damageValue))))
            {
                Log.Error("Failed to find patch location");
                return;
            }
            
            c.Goto(foundCursors[2].Next, MoveType.Before); // stfld GenericDamageOrb.damageValue

            c.Emit(OpCodes.Ldarg, damageInfoParameter);
            c.EmitDelegate<Func<float, DamageInfo, float>>(getOrbDamage);

            static float getOrbDamage(float damageValue, DamageInfo damageInfo)
            {
                CharacterBody attackerBody = damageInfo?.attacker ? damageInfo.attacker.GetComponent<CharacterBody>() : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                ItemQualityCounts missileVoid = ItemQualitiesContent.ItemQualityGroups.MissileVoid.GetItemCountsEffective(attackerInventory);
                if (missileVoid.TotalQualityCount > 0)
                {
                    float maxDamageCoefficient = (1f * missileVoid.UncommonCount) +
                                                 (2f * missileVoid.RareCount) +
                                                 (3f * missileVoid.EpicCount) +
                                                 (5f * missileVoid.LegendaryCount);

                    float shieldFraction = 0f;
                    if (attackerBody && attackerBody.healthComponent)
                    {
                        shieldFraction = attackerBody.healthComponent.shield / attackerBody.healthComponent.fullCombinedHealth;
                    }

                    float damageCoefficient = shieldFraction * maxDamageCoefficient;
                    if (damageCoefficient > 0)
                    {
                        damageValue += Util.OnHitProcDamage(damageInfo.damage, attackerBody.damage, damageCoefficient);
                    }
                }

                return damageValue;
            }
        }
    }
}
