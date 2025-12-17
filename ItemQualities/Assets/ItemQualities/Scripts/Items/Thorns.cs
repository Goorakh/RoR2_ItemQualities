using ItemQualities.Utilities.Extensions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Orbs;
using System;

namespace ItemQualities.Items
{
    static class Thorns
    {
        [SystemInitializer]
        static void Init()
        {
            IL.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        static void HealthComponent_TakeDamageProcess(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int damageReportLocalIndex = -1;
            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchNewobj<DamageReport>(),
                               x => x.MatchStloc(typeof(DamageReport), il, out damageReportLocalIndex),
                               x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.thorns)),
                               x => x.MatchStfld<LightningOrb>(nameof(LightningOrb.damageValue))))
            {
                Log.Error("Failed to find patch location");
                return;
            }

            c.Goto(foundCursors[3].Next, MoveType.AfterLabel);

            c.Emit(OpCodes.Ldloc, damageReportLocalIndex);
            c.EmitDelegate<Func<float, DamageReport, float>>(getThornsDamage);

            static float getThornsDamage(float thornsDamage, DamageReport damageReport)
            {
                Inventory victimInventory = damageReport?.victimBody ? damageReport.victimBody.inventory : null;

                ItemQualityCounts thorns = ItemQualitiesContent.ItemQualityGroups.Thorns.GetItemCountsEffective(victimInventory);
                if (thorns.TotalQualityCount > 0)
                {
                    float returnDamageCoefficient = (0.10f * thorns.UncommonCount) +
                                                    (0.15f * thorns.RareCount) +
                                                    (0.20f * thorns.EpicCount) +
                                                    (0.25f * thorns.LegendaryCount);

                    thornsDamage += returnDamageCoefficient * damageReport.damageDealt;
                }

                return thornsDamage;
            }
        }
    }
}
