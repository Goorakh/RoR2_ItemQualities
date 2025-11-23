using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class NearbyDamageBonus
    {
        static DamageColorIndex _nearbyBoostedColorIndex;

        [SystemInitializer]
        static void Init()
        {
            _nearbyBoostedColorIndex = ColorsAPI.RegisterDamageColor(new Color32(247, 59, 115, 255));

            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!il.Method.TryFindParameter<DamageInfo>(out ParameterDefinition damageInfoParameter))
            {
                Log.Error("Failed to find DamageInfo parameter");
                return;
            }

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.NearbyDamageBonus)),
                               x => x.MatchLdcR4(0.2f)))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After);

            c.Emit(OpCodes.Ldarg, damageInfoParameter);

            c.EmitDelegate<Func<float, DamageInfo, float>>(getFocusCrystalDamage);

            static float getFocusCrystalDamage(float damagePerFocusCrystal, DamageInfo damageInfo)
            {
                GameObject attacker = damageInfo?.attacker;
                CharacterBody attackerBody = attacker ? attacker.GetComponent<CharacterBody>() : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;
                TeamIndex attackerTeam = TeamComponent.GetObjectTeam(attacker);

                ItemQualityCounts nearbyDamageBonus = ItemQualitiesContent.ItemQualityGroups.NearbyDamageBonus.GetItemCountsEffective(attackerInventory);

                if (nearbyDamageBonus.TotalCount > nearbyDamageBonus.BaseItemCount)
                {
                    SphereSearch targetSearch = new SphereSearch()
                    {
                        origin = attackerBody.corePosition,
                        radius = 13f,
                        queryTriggerInteraction = QueryTriggerInteraction.Ignore,
                        mask = LayerIndex.entityPrecise.mask
                    };

                    targetSearch.RefreshCandidates();

                    TeamMask enemyTeams = TeamMask.all;
                    if (attackerTeam != TeamIndex.None)
                    {
                        enemyTeams = TeamMask.GetEnemyTeams(attackerTeam);
                    }

                    targetSearch.FilterCandidatesByHurtBoxTeam(enemyTeams);
                    targetSearch.FilterCandidatesByDistinctHurtBoxEntities();

                    int enemiesInRange = 0;

                    foreach (HurtBox hurtBox in targetSearch.GetHurtBoxes())
                    {
                        HealthComponent enemyHealthComponent = hurtBox ? hurtBox.healthComponent : null;
                        if (!enemyHealthComponent || !enemyHealthComponent.alive)
                            continue;

                        if (enemyHealthComponent.gameObject == attacker)
                            continue;

                        enemiesInRange++;
                    }

                    if (enemiesInRange == 1)
                    {
                        float damageBonus = (0.05f * nearbyDamageBonus.UncommonCount) +
                                            (0.15f * nearbyDamageBonus.RareCount) +
                                            (0.35f * nearbyDamageBonus.EpicCount) +
                                            (0.50f * nearbyDamageBonus.LegendaryCount);

                        if (damageBonus > 0)
                        {
                            damagePerFocusCrystal += damageBonus;
                            damageInfo.damageColorIndex = _nearbyBoostedColorIndex;
                        }
                    }
                }

                return damagePerFocusCrystal;
            }
        }

        delegate void ModifyFocusCrystalDamageDelegate(HealthComponent healthComponent, DamageInfo damageInfo, ref float damageValue);
    }
}
