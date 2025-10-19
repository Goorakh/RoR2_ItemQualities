using MonoMod.Cil;
using RoR2;
using System;

namespace ItemQualities.Items
{
    static class BleedOnHitVoid
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.DotController.InflictDot_refInflictDotInfo += DotController_InflictDot_refInflictDotInfo;
        }

        static void DotController_InflictDot_refInflictDotInfo(On.RoR2.DotController.orig_InflictDot_refInflictDotInfo orig, ref InflictDotInfo inflictDotInfo)
        {
            try
            {
                if (inflictDotInfo.dotIndex == DotController.DotIndex.Fracture)
                {
                    CharacterBody attackerBody = inflictDotInfo.attackerObject ? inflictDotInfo.attackerObject.GetComponent<CharacterBody>() : null;
                    Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                    ItemQualityCounts bleedOnHitVoid = default;
                    if (attackerInventory)
                    {
                        bleedOnHitVoid = ItemQualitiesContent.ItemQualityGroups.BleedOnHitVoid.GetItemCounts(attackerInventory);
                    }

                    if (bleedOnHitVoid.TotalQualityCount > 0)
                    {
                        float damageMultAdd = (0.10f * bleedOnHitVoid.UncommonCount) +
                                              (0.20f * bleedOnHitVoid.RareCount) +
                                              (0.30f * bleedOnHitVoid.EpicCount) +
                                              (0.50f * bleedOnHitVoid.LegendaryCount);

                        inflictDotInfo.damageMultiplier += damageMultAdd;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error_NoCallerPrefix(e);
            }

            orig(ref inflictDotInfo);
        }
    }
}
