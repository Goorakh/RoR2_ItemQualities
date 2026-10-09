using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Items;
using System;

namespace ItemQualities.Items
{
    internal static class Phasing
    {
        [SystemInitializer]
        private static void Init()
        {
            IL.RoR2.Items.PhasingBodyBehavior.FixedUpdate += PhasingBodyBehavior_FixedUpdate;

            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        private static void onServerDamageDealt(DamageReport damageReport)
        {
            if (damageReport?.damageInfo == null)
                return;

            if (!damageReport.victimBody || !damageReport.victimBody.inventory)
                return;

            if (damageReport.damageDealt > 0f && !damageReport.damageInfo.rejected)
            {
                ItemQualityCounts phasing = damageReport.victimBody.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.Phasing);
                if (phasing.TotalQualityCount > 0)
                {
                    float stealthProcChance = (5f * phasing.UncommonCount) +
                                              (15f * phasing.RareCount) +
                                              (30f * phasing.EpicCount) +
                                              (60f * phasing.LegendaryCount);

                    if (RollUtil.CheckRoll(stealthProcChance, damageReport.victimMaster, false) &&
                        !damageReport.victimBody.hasCloakBuff &&
                        damageReport.victimBody.TryGetComponent(out PhasingBodyBehavior phasingBodyBehavior))
                    {
                        phasingBodyBehavior.TriggerEffect();
                    }
                }
            }
        }

        private static void PhasingBodyBehavior_FixedUpdate(ILContext il)
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
                    if (healthComponent && healthComponent.TryGetComponentCached(out CharacterBodyExtraStatsTracker extraStatsTracker))
                    {
                        isHealthLow = healthComponent.IsHealthBelowThreshold(extraStatsTracker.StealthKitActivationThreshold);
                    }

                    return isHealthLow;
                }

                patchCount++;
            }

            if (patchCount == 0)
            {
                Log.PatchError(il, "Failed to find patch location");
            }
            else
            {
                Log.Debug($"Found {patchCount} patch location(s)");
            }
        }
    }
}
