using ItemQualities.Utilities.Extensions;
using RoR2;

namespace ItemQualities.Items
{
    internal static class BarrierOnCooldown
    {
        private static EffectIndex _barrierOnCooldownProcEffect = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        private static void Init()
        {
            _barrierOnCooldownProcEffect = EffectCatalogUtils.FindEffectIndex("BarrierOnCooldownProc");
            if (_barrierOnCooldownProcEffect == EffectIndex.Invalid)
            {
                Log.Error("Failed to find Eclipse Lite proc effect index");
            }

            EquipmentSlot.onServerEquipmentActivated += onServerEquipmentActivated;

            On.RoR2.HealthComponent.AddBarrier += HealthComponent_AddBarrier;
            On.RoR2.HealthComponent.AddCharge += HealthComponent_AddCharge;
        }

        private static void HealthComponent_AddCharge(On.RoR2.HealthComponent.orig_AddCharge orig, HealthComponent self, float value)
        {
            float defaultMax = self.body.maxBarrier;
            self.body.maxBarrier = getMaxBarrier(self.body);
            try
            {
                orig(self, value);
            }
            finally
            {
                self.body.maxBarrier = defaultMax;
            }
        }

        private static void HealthComponent_AddBarrier(On.RoR2.HealthComponent.orig_AddBarrier orig, HealthComponent self, float value)
        {
            float defaultMax = self.body.maxBarrier;
            self.body.maxBarrier = getMaxBarrier(self.body);
            try
            {
                orig(self, value);
            }
            finally
            {
                self.body.maxBarrier = defaultMax;
            }
        }

        private static float getMaxBarrier(CharacterBody body)
        {
            float maxBarrier = body.maxBarrier;

            if (body.inventory)
            {
                ItemQualityCounts barrierOnCooldown = body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BarrierOnCooldown);

                float barrierMultAdd = (barrierOnCooldown.UncommonCount * 0.3f) +
                                       (barrierOnCooldown.RareCount * 0.6f) +
                                       (barrierOnCooldown.EpicCount * 1f) +
                                       (barrierOnCooldown.LegendaryCount * 1.5f);

                if (barrierMultAdd > 0f)
                {
                    maxBarrier *= 1f + barrierMultAdd;
                }
            }

            return maxBarrier;
        }

        private static void onServerEquipmentActivated(EquipmentSlot equipmentSlot, EquipmentIndex equipmentIndex)
        {
            if (!equipmentSlot || equipmentIndex == EquipmentIndex.None)
                return;

            CharacterBody activatorBody = equipmentSlot.characterBody;
            Inventory activatorInventory = activatorBody ? activatorBody.inventory : null;
            if (!activatorInventory)
                return;

            ItemQualityCounts barrierOnCooldown = activatorInventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.BarrierOnCooldown);
            if (barrierOnCooldown.TotalQualityCount > 0)
            {
                float baseCooldown = EquipmentCatalog.GetEquipmentDef(equipmentIndex).cooldown;

                float barrierFractionPerSecondCooldown = (0.01f * barrierOnCooldown.UncommonCount) +
                                                         (0.02f * barrierOnCooldown.RareCount) +
                                                         (0.03f * barrierOnCooldown.EpicCount) +
                                                         (0.04f * barrierOnCooldown.LegendaryCount);

                activatorBody.healthComponent.AddBarrier(activatorBody.healthComponent.fullCombinedHealth * baseCooldown * barrierFractionPerSecondCooldown);

                if (_barrierOnCooldownProcEffect != EffectIndex.Invalid)
                {
                    EffectManager.SpawnEffect(_barrierOnCooldownProcEffect, new EffectData
                    {
                        origin = activatorBody.corePosition
                    }, true);
                }
            }
        }
    }
}
