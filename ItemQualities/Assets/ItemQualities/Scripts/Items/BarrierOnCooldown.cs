using ItemQualities.Utilities.Extensions;
using RoR2;

namespace ItemQualities.Items
{
    static class BarrierOnCooldown
    {
        static EffectIndex _barrierOnCooldownProcEffect = EffectIndex.Invalid;

        [SystemInitializer(typeof(EffectCatalogUtils))]
        static void Init()
        {
            _barrierOnCooldownProcEffect = EffectCatalogUtils.FindEffectIndex("BarrierOnCooldownProc");
            if (_barrierOnCooldownProcEffect == EffectIndex.Invalid)
            {
                Log.Error("Failed to find Eclipse Lite proc effect index");
            }

            EquipmentSlot.onServerEquipmentActivated += onServerEquipmentActivated;
        }

        static void onServerEquipmentActivated(EquipmentSlot equipmentSlot, EquipmentIndex equipmentIndex)
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

                float barrierFractionPerSecondCooldown = (0.005f * barrierOnCooldown.UncommonCount) +
                                                         (0.010f * barrierOnCooldown.RareCount) +
                                                         (0.030f * barrierOnCooldown.EpicCount) +
                                                         (0.050f * barrierOnCooldown.LegendaryCount);

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
