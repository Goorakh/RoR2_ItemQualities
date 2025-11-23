using R2API;
using RoR2;
using UnityEngine.Networking;

namespace ItemQualities.Items
{
    static class KillEliteFrenzy
    {
        [SystemInitializer]
        static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
            GlobalEventManager.onCharacterDeathGlobal += onCharacterDeathGlobal;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender)
                return;

            ItemQualityCounts killEliteFrenzy = ItemQualitiesContent.ItemQualityGroups.KillEliteFrenzy.GetItemCountsEffective(sender.inventory);
            BuffQualityCounts killEliteFrenzyBuff = ItemQualitiesContent.BuffQualityGroups.KillEliteFrenzyBuff.GetBuffCounts(sender);
            if (killEliteFrenzy.TotalQualityCount > 0 && killEliteFrenzyBuff.TotalQualityCount > 0)
            {
                float damagePerBuff = (0.10f * killEliteFrenzy.UncommonCount) +
                                      (0.20f * killEliteFrenzy.RareCount) +
                                      (0.40f * killEliteFrenzy.EpicCount) +
                                      (0.60f * killEliteFrenzy.LegendaryCount);

                if (damagePerBuff > 0f)
                {
                    args.damageMultAdd += damagePerBuff * killEliteFrenzyBuff.TotalQualityCount;
                }
            }
        }

        static void onCharacterDeathGlobal(DamageReport deathReport)
        {
            if (!NetworkServer.active || deathReport == null)
                return;

            if (deathReport.attackerBody && deathReport.victimIsElite)
            {
                ItemQualityCounts killEliteFrenzy = ItemQualitiesContent.ItemQualityGroups.KillEliteFrenzy.GetItemCountsEffective(deathReport.attackerBody.inventory);
                if (killEliteFrenzy.TotalQualityCount > 0 && deathReport.attackerBody.HasBuff(RoR2Content.Buffs.NoCooldowns))
                {
                    deathReport.attackerBody.AddBuff(ItemQualitiesContent.BuffQualityGroups.KillEliteFrenzyBuff.GetBuffIndex(killEliteFrenzy.HighestQuality));
                }
            }
        }
    }
}
