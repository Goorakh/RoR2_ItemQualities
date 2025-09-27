using RoR2;

namespace ItemQualities.Items
{
    static class BleedOnHit
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.DotController.InflictDot_refInflictDotInfo += DotController_InflictDot_refInflictDotInfo;
        }

        static void DotController_InflictDot_refInflictDotInfo(On.RoR2.DotController.orig_InflictDot_refInflictDotInfo orig, ref InflictDotInfo inflictDotInfo)
        {
            if (inflictDotInfo.dotIndex == DotController.DotIndex.Bleed)
            {
                CharacterBody attackerBody = inflictDotInfo.attackerObject ? inflictDotInfo.attackerObject.GetComponent<CharacterBody>() : null;
                Inventory attackerInventory = attackerBody ? attackerBody.inventory : null;

                ItemQualityCounts bleedOnHit = default;
                if (attackerInventory)
                {
                    bleedOnHit = ItemQualitiesContent.ItemQualityGroups.BleedOnHit.GetItemCounts(attackerInventory);
                }

                float damageMultAdd = (0.10f * bleedOnHit.UncommonCount) +
                                      (0.20f * bleedOnHit.RareCount) +
                                      (0.30f * bleedOnHit.EpicCount) +
                                      (0.50f * bleedOnHit.LegendaryCount);

                inflictDotInfo.damageMultiplier += damageMultAdd;
            }

            orig(ref inflictDotInfo);
        }
    }
}
