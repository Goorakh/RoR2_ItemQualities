using MonoMod.Cil;
using R2API;
using RoR2;

namespace ItemQualities.Items
{
    static class FragileDamageBonus
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;

            IL.RoR2.HealthComponent.UpdateLastHitTime += HealthComponent_UpdateLastHitTime;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            ItemQualityCounts fragileDamageBonus = ItemQualitiesContent.ItemQualityGroups.FragileDamageBonus.GetItemCountsEffective(sender.inventory);
            if (fragileDamageBonus.TotalQualityCount > 0)
            {
                BuffQualityCounts fragileDamageBonusBuff = ItemQualitiesContent.BuffQualityGroups.FragileDamageBonusBuff.GetBuffCounts(sender);
                if (fragileDamageBonusBuff.TotalQualityCount > 0)
                {
                    float damageBonusPerBuff = (0.05f * fragileDamageBonus.UncommonCount) +
                                               (0.10f * fragileDamageBonus.RareCount) +
                                               (0.15f * fragileDamageBonus.EpicCount) +
                                               (0.20f * fragileDamageBonus.LegendaryCount);

                    args.damageMultAdd += damageBonusPerBuff;
                }
            }
        }

        static void HealthComponent_UpdateLastHitTime(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (!c.TryFindNext(out ILCursor[] foundCursors,
                               x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.fragileDamageBonus)),
                               x => x.MatchCallOrCallvirt(typeof(HealthComponent).GetProperty(nameof(HealthComponent.isHealthLow)).GetMethod),
                               x => x.MatchCallOrCallvirt<Inventory.ItemTransformation>(nameof(Inventory.ItemTransformation.TryTransform))))
            {
                Log.Error("Failed to find watch break location");
                return;
            }

            c.Goto(foundCursors[2].Next, MoveType.After); // call Inventory.ItemTransformation.TryTransform

            ItemHooks.EmitCombinedQualityItemTransformationPatch(c);
        }
    }
}
