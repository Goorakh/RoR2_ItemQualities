using R2API;
using RoR2;

namespace ItemQualities.Items
{
    static class StunChanceOnHit
    {
        static EffectIndex _stunGrenadeImpactIndex = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        static void Init()
        {
            _stunGrenadeImpactIndex = EffectCatalogUtils.FindEffectIndex("ImpactStunGrenade");
            if (_stunGrenadeImpactIndex == EffectIndex.Invalid)
            {
                Log.Warning("Failed to find stun grenade impact effect index");
            }

            RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
            GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
        }

        static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender || args == null)
                return;

            int bossStunCount = sender.GetBuffCount(ItemQualitiesContent.Buffs.BossStun);

            if (bossStunCount > 0)
            {
                args.attackSpeedReductionMultAdd += 0.2f * bossStunCount;
            }
        }

        static void onServerDamageDealt(DamageReport damageReport)
        {
            if (damageReport == null)
                return;

            HealthComponent victim = damageReport.victim;
            CharacterBody victimBody = victim ? victim.body : null;
            if (!victim || !victimBody || !victimBody.isChampion)
                return;

            CharacterMaster attackerMaster = damageReport.attackerMaster;
            Inventory attackerInventory = attackerMaster ? attackerMaster.inventory : null;

            ItemQualityCounts stunChanceOnHit = ItemQualitiesContent.ItemQualityGroups.StunChanceOnHit.GetItemCounts(attackerInventory);

            if (stunChanceOnHit.TotalQualityCount <= 0)
                return;

            float stunChance = Util.ConvertAmplificationPercentageIntoReductionPercentage(SetStateOnHurt.stunChanceOnHitBaseChancePercent * stunChanceOnHit.TotalCount * damageReport.damageInfo.procCoefficient);
            if (Util.CheckRoll(stunChance, attackerMaster))
            {
                int bossStunCount = (1 * stunChanceOnHit.UncommonCount) +
                                    (2 * stunChanceOnHit.RareCount) +
                                    (4 * stunChanceOnHit.EpicCount) +
                                    (7 * stunChanceOnHit.LegendaryCount);

                const float StunDuration = 1.5f;

                foreach (CharacterBody.TimedBuff timedBuff in victimBody.timedBuffs)
                {
                    if (timedBuff.buffIndex == ItemQualitiesContent.Buffs.BossStun.buffIndex)
                    {
                        if (timedBuff.timer < StunDuration)
                        {
                            timedBuff.timer = StunDuration;
                            timedBuff.totalDuration = StunDuration;
                        }
                    }
                }

                for (int i = 0; i < bossStunCount; i++)
                {
                    victimBody.AddTimedBuff(ItemQualitiesContent.Buffs.BossStun, StunDuration);
                }

                if (_stunGrenadeImpactIndex != EffectIndex.Invalid)
                {
                    EffectManager.SpawnEffect(_stunGrenadeImpactIndex, new EffectData
                    {
                        origin = damageReport.damageInfo.position,
                        rotation = Util.QuaternionSafeLookRotation(-damageReport.damageInfo.force)
                    }, true);
                }
            }
        }
    }
}
