using ItemQualities.Utilities.Extensions;
using RoR2;

namespace ItemQualities.Items
{
    internal static class DroneWeapons
    {
        [InitDuringStartupPhase(GameInitPhase.PostProgressBar)]
        private static void Init()
        {
            On.RoR2.DroneWeaponsBehavior.OnMasterSpawned += DroneWeaponsBehavior_OnMasterSpawned;
        }

        private static void DroneWeaponsBehavior_OnMasterSpawned(On.RoR2.DroneWeaponsBehavior.orig_OnMasterSpawned orig, DroneWeaponsBehavior self, SpawnCard.SpawnResult spawnResult)
        {
            orig(self, spawnResult);

            if (!spawnResult.success)
                return;

            if (!self.body && !self.body.inventory)
                return;

            QualityTier qualityTier = self.body.inventory.GetItemCountsEffective(ItemQualitiesContent.ItemQualityGroups.DroneWeapons).HighestQuality;
            if (qualityTier == QualityTier.None)
                return;

            if (spawnResult.spawnedInstance && spawnResult.spawnedInstance.TryGetComponent(out CharacterMaster master))
            {
                master.inventory.GiveItemPermanent(ItemQualitiesContent.ItemQualityGroups.QualityTier.GetItemIndex(qualityTier));
            }
        }
    }
}
