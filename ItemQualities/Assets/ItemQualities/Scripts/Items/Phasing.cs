using ItemQualities.Utilities;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class Phasing
    {
        static EffectIndex _stealthKitProcEffectIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        static void Init()
        {
            _stealthKitProcEffectIndex = EffectCatalogUtils.FindEffectIndex("ProcStealthkit");
            if (_stealthKitProcEffectIndex == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find stealthkit proc effect index");
            }

            IL.RoR2.Items.PhasingBodyBehavior.FixedUpdate += PhasingBodyBehavior_FixedUpdate;

            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        static void onServerDamageDealt(DamageReport damageReport)
        {
            if (damageReport?.damageInfo == null)
                return;

            if (!damageReport.victimBody)
                return;

            if (damageReport.damageDealt > 0f && !damageReport.damageInfo.rejected)
            {
                ItemQualityCounts phasing = ItemQualitiesContent.ItemQualityGroups.Phasing.GetItemCountsEffective(damageReport.victimBody.inventory);
                if (phasing.TotalQualityCount > 0)
                {
                    float stealthProcChance = (5f * phasing.UncommonCount) +
                                              (15f * phasing.RareCount) +
                                              (30f * phasing.EpicCount) +
                                              (60f * phasing.LegendaryCount);

                    if (RollUtil.CheckRoll(stealthProcChance, damageReport.victimMaster, false) && !damageReport.victimBody.hasCloakBuff)
                    {
                        damageReport.victimBody.AddTimedBuff(RoR2Content.Buffs.Cloak, 5f);
                        damageReport.victimBody.AddTimedBuff(RoR2Content.Buffs.CloakSpeed, 5f);

                        if (_stealthKitProcEffectIndex != EffectIndex.Invalid)
                        {
                            EffectManager.SpawnEffect(_stealthKitProcEffectIndex, new EffectData
                            {
                                origin = damageReport.victimBody.corePosition,
                                rotation = Quaternion.identity
                            }, true);
                        }
                    }
                }
            }
        }

        static void PhasingBodyBehavior_FixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int patchCount = 0;

            while (c.TryGotoNext(MoveType.Before,
                                 x => x.MatchCallOrCallvirt(typeof(HealthComponent).GetProperty(nameof(HealthComponent.isHealthLow)).GetMethod)))
            {
                c.Emit(OpCodes.Dup);
                c.Index++;

                c.EmitDelegate<Func<HealthComponent, bool, bool>>(isUnderStealthKitThreshold);

                static bool isUnderStealthKitThreshold(HealthComponent healthComponent, bool isHealthLow)
                {
                    if (healthComponent && healthComponent.TryGetComponent(out CharacterBodyExtraStatsTracker extraStatsTracker))
                    {
                        isHealthLow = healthComponent.IsHealthBelowThreshold(extraStatsTracker.StealthKitActivationThreshold);
                    }

                    return isHealthLow;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.Error("Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }
    }
}
