using ItemQualities.Utilities.Extensions;
using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
    static class DroneWeapons
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.CharacterMaster.GetDeployableSameSlotLimit += CharacterMaster_GetDeployableSameSlotLimit;
            On.RoR2.DroneWeaponsBehavior.FixedUpdate += DroneWeaponsBehavior_FixedUpdate;
            On.RoR2.DroneWeaponsBehavior.TrySpawnDrone += DroneWeaponsBehavior_TrySpawnDrone;
            On.RoR2.DroneWeaponsBehavior.OnMasterSpawned += DroneWeaponsBehavior_OnMasterSpawned;
        }

        static void DroneWeaponsBehavior_OnMasterSpawned(On.RoR2.DroneWeaponsBehavior.orig_OnMasterSpawned orig, DroneWeaponsBehavior self, SpawnCard.SpawnResult spawnResult)
        {
            orig(self, spawnResult);

            if (!self.body)
                return;

            ItemQualityCounts droneWeapons = default;
            if (self.body.inventory)
            {
                droneWeapons = self.body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.DroneWeapons);
            }

            if (droneWeapons.TotalQualityCount > 0)
            {
                self.hasSpawnedDrone = false;
            }

            GameObject spawnedInstance = spawnResult.spawnedInstance;
            if (!spawnedInstance)
                return;

            CharacterMaster droneMaster = spawnedInstance.GetComponent<CharacterMaster>();
            if (!droneMaster)
                return;

            GameObject droneBodyObject = droneMaster.bodyInstanceObject;
            if (!droneBodyObject)
                return;

            Deployable deployable = droneBodyObject.GetComponent<Deployable>();
            if (deployable)
            {
                self.body.master.AddDeployable(deployable, DeployableSlot.DroneWeaponsDrone);
            }
        }

        static void DroneWeaponsBehavior_TrySpawnDrone(On.RoR2.DroneWeaponsBehavior.orig_TrySpawnDrone orig, DroneWeaponsBehavior self)
        {
            orig(self);

            if (self.body && self.body.inventory)
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

        static int CharacterMaster_GetDeployableSameSlotLimit(On.RoR2.CharacterMaster.orig_GetDeployableSameSlotLimit orig, CharacterMaster self, DeployableSlot slot)
        {
            int result = orig(self, slot);

            if (slot == DeployableSlot.DroneWeaponsDrone && self.inventory)
            {
                ItemQualityCounts droneWeapons = self.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.DroneWeapons);

                if (droneWeapons.TotalQualityCount > 0)
                {
                    result += (droneWeapons.UncommonCount * 2) +
                              (droneWeapons.RareCount * 3) +
                              (droneWeapons.EpicCount * 4) +
                              (droneWeapons.LegendaryCount * 5) - 1;
                }
            }

            return result;
        }

        static void DroneWeaponsBehavior_FixedUpdate(On.RoR2.DroneWeaponsBehavior.orig_FixedUpdate orig, DroneWeaponsBehavior self)
        {
            orig(self);

            if (self.body && self.body.inventory && self.body.master && self.body.master.IsDeployableLimited(DeployableSlot.DroneWeaponsDrone))
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
}
