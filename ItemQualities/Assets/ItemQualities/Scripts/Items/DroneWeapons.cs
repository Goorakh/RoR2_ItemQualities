using ItemQualities.Utilities.Extensions;
using RoR2;

namespace ItemQualities.Items
{
    static class DroneWeapons
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.CharacterMaster.GetDeployableSameSlotLimit += CharacterMaster_GetDeployableSameSlotLimit;
            On.RoR2.DroneWeaponsBehavior.OnMasterSpawned += DroneWeaponsBehavior_OnMasterSpawned;
        }

        static void DroneWeaponsBehavior_OnMasterSpawned(On.RoR2.DroneWeaponsBehavior.orig_OnMasterSpawned orig, DroneWeaponsBehavior self, SpawnCard.SpawnResult spawnResult)
        {
            orig(self, spawnResult);

            if (!self.body || !self.body.master)
                return;

            if (!self.body.master.IsDeployableLimited(DeployableSlot.DroneDynamiteDrone))
            {
                self.hasSpawnedDrone = false;

                if (spawnResult.success && self.body.inventory)
                {
                    ItemQualityCounts droneWeapons = self.body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.DroneWeapons);
                    switch (droneWeapons.HighestQuality)
                    {
                        case QualityTier.Uncommon:
                            self.spawnDelay = 120f;
                            break;
                        case QualityTier.Rare:
                            self.spawnDelay = 90f;
                            break;
                        case QualityTier.Epic:
                            self.spawnDelay = 45f;
                            break;
                        case QualityTier.Legendary:
                            self.spawnDelay = 1f;
                            break;
                    }
                }
            }
        }

        static int CharacterMaster_GetDeployableSameSlotLimit(On.RoR2.CharacterMaster.orig_GetDeployableSameSlotLimit orig, CharacterMaster self, DeployableSlot slot)
        {
            int result = orig(self, slot);

            if (slot == DeployableSlot.DroneWeaponsDrone && self.inventory)
            {
                ItemQualityCounts droneWeapons = self.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.DroneWeapons);

                if (droneWeapons.TotalQualityCount > 0)
                {
                    result += (2 * droneWeapons.UncommonCount) +
                              (3 * droneWeapons.RareCount) +
                              (4 * droneWeapons.EpicCount) +
                              (5 * droneWeapons.LegendaryCount) - 1;
                }
            }

            return result;
        }
    }
}
