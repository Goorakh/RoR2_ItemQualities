using ItemQualities.Utilities.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class Tooth
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
        }

        static void GlobalEventManager_OnCharacterDeath(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!il.Method.TryFindParameter<DamageReport>(out ParameterDefinition damageReportParameter))
            {
                Log.Error("Failed to find DamageReport parameter");
                return;
            }

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.Tooth)),
                               x => x.MatchCallOrCallvirt(typeof(LegacyResourcesAPI), nameof(LegacyResourcesAPI.Load)),
                               x => x.MatchCallOrCallvirt(typeof(UnityEngine.Object), nameof(UnityEngine.Object.Instantiate))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[1].Next, MoveType.After);

            VariableDefinition orbParametersVar = il.AddVariable<ToothOrbParameters>();

            c.Emit(OpCodes.Ldarg, damageReportParameter);
            c.Emit(OpCodes.Ldloca, orbParametersVar);
            c.EmitDelegate<GetHealingOrbPrefabDelegate>(getHealingOrbPrefab);

            static GameObject getHealingOrbPrefab(GameObject healingOrbPrefab, DamageReport damageReport, out ToothOrbParameters orbParameters)
            {
                float flatOrbValue = 0f;
                float fractionalOrbValue = 0f;
                BuffDef orbBuff = null;

                if (damageReport?.damageInfo != null)
                {
                    CharacterMaster attackerMaster = damageReport.attackerMaster;
                    Inventory attackerInventory = attackerMaster ? attackerMaster.inventory : null;

                    ItemQualityCounts tooth = ItemQualitiesContent.ItemQualityGroups.Tooth.GetItemCounts(attackerInventory);
                    if (tooth.TotalQualityCount > 0)
                    {
                        GameObject overrideHealOrbPrefab = null;

                        DamageSource damageSource = damageReport.damageInfo.damageType.damageSource;
                        if ((damageSource & DamageSource.Primary) != 0)
                        {
                            overrideHealOrbPrefab = ItemQualitiesContent.NetworkedPrefabs.HealOrbPrimary;

                            float buffDuration = (1f * tooth.UncommonCount) +
                                                 (2f * tooth.RareCount) +
                                                 (3f * tooth.EpicCount) +
                                                 (5f * tooth.LegendaryCount);

                            flatOrbValue = buffDuration;

                            orbBuff = BuffCatalog.GetBuffDef(ItemQualitiesContent.BuffQualityGroups.ToothPrimaryBuff.GetBuffIndex(tooth.HighestQuality));
                        }
                        else if ((damageSource & DamageSource.Secondary) != 0)
                        {
                            overrideHealOrbPrefab = ItemQualitiesContent.NetworkedPrefabs.HealOrbSecondary;

                            float buffDuration = (2f * tooth.UncommonCount) +
                                                 (4f * tooth.RareCount) +
                                                 (6f * tooth.EpicCount) +
                                                 (10f * tooth.LegendaryCount);

                            flatOrbValue = buffDuration;

                            orbBuff = BuffCatalog.GetBuffDef(ItemQualitiesContent.BuffQualityGroups.ToothSecondaryBuff.GetBuffIndex(tooth.HighestQuality));
                        }
                        else if ((damageSource & DamageSource.Utility) != 0)
                        {
                            overrideHealOrbPrefab = ItemQualitiesContent.NetworkedPrefabs.HealOrbUtility;

                            float flatBarrier = (15f * tooth.UncommonCount) +
                                                (25f * tooth.RareCount) +
                                                (35f * tooth.EpicCount) +
                                                (50f * tooth.LegendaryCount);

                            flatOrbValue = flatBarrier;
                        }
                        else if ((damageSource & DamageSource.Special) != 0)
                        {
                            overrideHealOrbPrefab = ItemQualitiesContent.NetworkedPrefabs.HealOrbSpecial;

                            float flatCooldownReduction = (1f * tooth.UncommonCount) +
                                                          (2f * tooth.RareCount) +
                                                          (3f * tooth.EpicCount) +
                                                          (5f * tooth.LegendaryCount);

                            flatOrbValue = flatCooldownReduction;
                        }

                        if (overrideHealOrbPrefab)
                        {
                            healingOrbPrefab = overrideHealOrbPrefab;
                        }
                    }
                }

                orbParameters = new ToothOrbParameters(flatOrbValue, fractionalOrbValue, orbBuff);
                return healingOrbPrefab;
            }

            c.Goto(foundCursors[2].Next, MoveType.After); // call UnityEngine.Object.Instantiate

            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Ldloc, orbParametersVar);
            c.EmitDelegate<Action<GameObject, ToothOrbParameters>>(setupHealingOrb);

            static void setupHealingOrb(GameObject healingOrb, ToothOrbParameters orbParameters)
            {
                if (healingOrb && (orbParameters.FlatValue > 0f || orbParameters.FractionalValue > 0f))
                {
                    BuffPickup buffPickup = healingOrb.GetComponentInChildren<BuffPickup>();
                    if (buffPickup)
                    {
                        buffPickup.buffDuration = orbParameters.FlatValue;
                        buffPickup.buffDef = orbParameters.Buff;
                    }

                    BarrierPickup barrierPickup = healingOrb.GetComponentInChildren<BarrierPickup>();
                    if (barrierPickup)
                    {
                        barrierPickup.FlatAmount = orbParameters.FlatValue;
                        barrierPickup.FractionalAmount = orbParameters.FractionalValue;
                    }

                    SkillCooldownPickup skillCooldownPickup = healingOrb.GetComponentInChildren<SkillCooldownPickup>();
                    if (skillCooldownPickup)
                    {
                        skillCooldownPickup.FlatAmount = orbParameters.FlatValue;
                        skillCooldownPickup.FractionalAmount = orbParameters.FractionalValue;
                    }
                }
            }
        }

        delegate GameObject GetHealingOrbPrefabDelegate(GameObject healingOrbPrefab, DamageReport damageReport, out ToothOrbParameters orbParameters);

        readonly struct ToothOrbParameters
        {
            public readonly float FlatValue;
            public readonly float FractionalValue;
            public readonly BuffDef Buff;

            public ToothOrbParameters(float flatValue, float fractionalValue, BuffDef buff)
            {
                FlatValue = flatValue;
                FractionalValue = fractionalValue;
                Buff = buff;
            }
        }
    }
}
