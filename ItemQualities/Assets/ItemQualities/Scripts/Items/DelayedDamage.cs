using ItemQualities.Utilities;
using ItemQualities.Utilities.Extensions;
using R2API;
using RoR2;
using System;
using System.Collections;
using UnityEngine;

namespace ItemQualities.Items
{
    static class DelayedDamage
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        static void HealthComponent_TakeDamageProcess(On.RoR2.HealthComponent.orig_TakeDamageProcess orig, HealthComponent self, DamageInfo damageInfo)
        {
            try
            {
                CharacterBody victimBody = self ? self.body : null;
                CharacterMaster victimMaster = victimBody ? victimBody.master : null;
                Inventory victimInventory = victimBody ? victimBody.inventory : null;

                if (victimInventory)
                {
                    ItemQualityCounts delayedDamage = victimInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.DelayedDamage);

                    if (delayedDamage.TotalQualityCount > 0 &&
                        !damageInfo.rejected &&
                        damageInfo.delayedDamageSecondHalf &&
                        !damageInfo.damageType.HasModdedDamageType(DamageTypes.ProcOnly))
                    {
                        float repeatProcsChance = (30f * delayedDamage.UncommonCount) +
                                                  (60f * delayedDamage.RareCount) +
                                                  (100f * delayedDamage.EpicCount) +
                                                  (150f * delayedDamage.LegendaryCount);

                        int repeatProcsCount = RollUtil.GetOverflowRoll(repeatProcsChance, victimMaster, false);
                        if (repeatProcsCount > 0)
                        {
                            DamageInfo[] repeatDamageInfos = new DamageInfo[repeatProcsCount];
                            for (int i = 0; i < repeatDamageInfos.Length; i++)
                            {
                                DamageInfo repeatDamageInfo = damageInfo.ShallowCopy();
                                repeatDamageInfo.delayedDamageSecondHalf = true;
                                repeatDamageInfo.firstHitOfDelayedDamageSecondHalf = false;
                                repeatDamageInfos[i] = repeatDamageInfo;
                            }

                            self.StartCoroutine(inflictRepeatProcs(self, repeatDamageInfos));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Failed to execute repeat damage hook: " + e);
            }

            orig(self, damageInfo);
        }

        static IEnumerator inflictRepeatProcs(HealthComponent victim, DamageInfo[] repeatDamageInfos)
        {
            const float TotalDelay = 0.3f;

            foreach (DamageInfo repeatDamageInfo in repeatDamageInfos)
            {
                yield return new WaitForSeconds(TotalDelay / repeatDamageInfos.Length);

                if (!victim)
                    break;

                repeatDamageInfo.damageType |= DamageType.BypassBlock | DamageType.Silent;
                repeatDamageInfo.damageType.AddModdedDamageType(DamageTypes.ProcOnly);
                victim.TakeDamage(repeatDamageInfo);
            }
        }
    }
}
