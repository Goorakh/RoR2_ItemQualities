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
            IL.RoR2.CharacterBody.UpdateDelayedDamage += ItemHooks.CombineGroupedItemCountsPatch;

            On.RoR2.HealthComponent.TakeDamageProcess += HealthComponent_TakeDamageProcess;
        }

        static void HealthComponent_TakeDamageProcess(On.RoR2.HealthComponent.orig_TakeDamageProcess orig, HealthComponent self, DamageInfo damageInfo)
        {
            bool invokedOrig = false;

            try
            {
                int repeatProcsCount = 0;

                CharacterBody victimBody = self ? self.body : null;

                CharacterMaster victimMaster = null;
                Inventory victimInventory = null;
                if (victimBody)
                {
                    victimMaster = victimBody.master;
                    victimInventory = victimBody.inventory;
                }

                ItemQualityCounts delayedDamage = ItemQualitiesContent.ItemQualityGroups.DelayedDamage.GetItemCounts(victimInventory);

                if (delayedDamage.TotalQualityCount > 0 &&
                    !damageInfo.rejected &&
                    damageInfo.delayedDamageSecondHalf &&
                    !damageInfo.damageType.HasModdedDamageType(DamageTypes.ProcOnly))
                {
                    float repeatProcsChance = (10f * delayedDamage.UncommonCount) +
                                              (30f * delayedDamage.RareCount) +
                                              (50f * delayedDamage.EpicCount) +
                                              (100f * delayedDamage.LegendaryCount);

                    repeatProcsCount = RollUtil.GetOverflowRoll(repeatProcsChance, victimBody ? victimBody.master : null);
                }

                DamageInfo[] repeatDamageInfos = Array.Empty<DamageInfo>();
                if (repeatProcsCount > 0)
                {
                    repeatDamageInfos = new DamageInfo[repeatProcsCount];
                    for (int i = 0; i < repeatDamageInfos.Length; i++)
                    {
                        DamageInfo repeatDamageInfo = damageInfo.ShallowCopy();
                        repeatDamageInfo.delayedDamageSecondHalf = true;
                        repeatDamageInfo.firstHitOfDelayedDamageSecondHalf = false;
                        repeatDamageInfos[i] = repeatDamageInfo;
                    }
                }

                invokedOrig = true;
                orig(self, damageInfo);

                if (repeatDamageInfos.Length > 0)
                {
                    self.StartCoroutine(inflictRepeatProcs(self, repeatDamageInfos));
                }
            }
            catch (Exception e)
            {
                Log.Error("Failed to execute repeat damage hook: " + e);

                if (!invokedOrig)
                {
                    orig(self, damageInfo);
                }
            }
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
