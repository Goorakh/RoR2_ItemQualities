using HG;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ItemQualities.Items
{
    internal static class NearbyDamageBonus
    {
        private static readonly SphereSearch _sharedNearbyTargetSearch = new SphereSearch
        {
            radius = 13f,
            queryTriggerInteraction = QueryTriggerInteraction.Ignore,
            mask = LayerIndex.entityPrecise.mask,
        };

        private static DamageColorIndex _nearbyBoostedColorIndex;

        [SystemInitializer]
        private static void Init()
        {
            _nearbyBoostedColorIndex = ColorsAPI.RegisterDamageColor(new Color32(247, 59, 115, 255));

            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        private static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.PatchError(il, "Failed to find DamageInfo parameter");
                return;
            }

            ILCursor c = new ILCursor(il);

            if (!ItemHooks.TryGotoNextItemCountVariable(c, typeof(RoR2Content.Items), nameof(RoR2Content.Items.NearbyDamageBonus), out VariableDefinition nearbyDamageBonusCountVar))
            {
                Log.PatchError(il, "Failed to find NearbyDamageBonus itemCount variable");
                return;
            }

            /*
             *  // (float)itemCountEffective4 * 0.2f
             *  IL_0C8A: ldloc.s   V_41
             *  IL_0C8C: conv.r4
             *  IL_0C8D: ldc.r4    0.2
             *  IL_0C92: mul
             */

            Instruction nearbyDamageBonusCoefficientInstruction = null;
            if (!c.TryGotoNext(MoveType.After,
                               x => x.MatchLdloc(nearbyDamageBonusCountVar),
                               x => x.MatchConvR4(),
                               x => x.MatchLdcR4(out _) && x.MatchAny(out nearbyDamageBonusCoefficientInstruction)))
            {
                Log.PatchError(il, "Failed to find patch location");
                return;
            }

            c.Goto(nearbyDamageBonusCoefficientInstruction, MoveType.After);

            c.Emit(OpCodes.Ldarg, damageInfoParameter);

            c.EmitDelegate<Func<float, DamageInfo, float>>(getFocusCrystalDamage);

            static float getFocusCrystalDamage(float damagePerFocusCrystal, DamageInfo damageInfo)
            {
                GameObject attacker = damageInfo?.attacker;
                CharacterBody attackerBody = attacker ? attacker.GetComponent<CharacterBody>() : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;
                TeamIndex attackerTeam = TeamComponent.GetObjectTeam(attacker);

                if (attackerInventory)
                {
                    ItemQualityCounts nearbyDamageBonus = attackerInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.NearbyDamageBonus);
                    if (nearbyDamageBonus.TotalQualityCount > 0)
                    {
                        SphereSearch targetSearch = _sharedNearbyTargetSearch;

                        targetSearch.origin = attackerBody.corePosition;

                        targetSearch.RefreshCandidates();

                        TeamMask enemyTeams = TeamMask.all;
                        if (attackerTeam != TeamIndex.None)
                        {
                            enemyTeams = TeamMask.GetEnemyTeams(attackerTeam);
                        }

                        targetSearch.FilterCandidatesByHurtBoxTeam(enemyTeams);
                        targetSearch.FilterCandidatesByDistinctHurtBoxEntities();

                        int enemiesInRange = 0;

                        using var _ = ListPool<HurtBox>.RentCollection(out List<HurtBox> hurtBoxes);
                        targetSearch.GetHurtBoxes(hurtBoxes);

                        foreach (HurtBox hurtBox in hurtBoxes)
                        {
                            HealthComponent enemyHealthComponent = hurtBox ? hurtBox.healthComponent : null;
                            if (!enemyHealthComponent || !enemyHealthComponent.alive)
                                continue;

                            if (ReferenceEquals(enemyHealthComponent.gameObject, attacker))
                                continue;

                            enemiesInRange++;
                        }

                        if (enemiesInRange > 0)
                        {
                            float damageBonus = (0.10f * nearbyDamageBonus.UncommonCount) +
                                                (0.20f * nearbyDamageBonus.RareCount) +
                                                (0.35f * nearbyDamageBonus.EpicCount) +
                                                (0.50f * nearbyDamageBonus.LegendaryCount);

                            damageBonus /= Mathf.Pow(2f, enemiesInRange);

                            if (damageBonus > 0)
                            {
                                damagePerFocusCrystal += damageBonus;
                                damageInfo.damageColorIndex = _nearbyBoostedColorIndex;
                            }
                        }
                    }
                }

                return damagePerFocusCrystal;
            }
        }

        private delegate void ModifyFocusCrystalDamageDelegate(HealthComponent healthComponent, DamageInfo damageInfo, ref float damageValue);
    }
}
