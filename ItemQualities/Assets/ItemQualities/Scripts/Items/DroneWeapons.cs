using RoR2;
using UnityEditor.Graphs;
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
			self.hasSpawnedDrone = false;
			GameObject spawnedInstance = spawnResult.spawnedInstance;
			if (!spawnedInstance) return;
			CharacterMaster DroneMaster = spawnedInstance.GetComponent<CharacterMaster>();
			if(!DroneMaster) return;
			GameObject body = DroneMaster.bodyInstanceObject;
			if(!body) return;
			Deployable deployable = body.GetComponent<Deployable>();
			if (deployable)
			{
				self.body.master.AddDeployable(deployable, DeployableSlot.DroneWeaponsDrone);
			}
		}

		static void DroneWeaponsBehavior_TrySpawnDrone(On.RoR2.DroneWeaponsBehavior.orig_TrySpawnDrone orig, DroneWeaponsBehavior self)
		{
			orig(self);
			switch (ItemQualitiesContent.ItemQualityGroups.DroneWeapons.GetHighestQualityInInventory(self.body.master.inventory))
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

		static int CharacterMaster_GetDeployableSameSlotLimit(On.RoR2.CharacterMaster.orig_GetDeployableSameSlotLimit orig, CharacterMaster self, DeployableSlot slot)
		{
			int result = orig(self, slot);
			if (slot == DeployableSlot.DroneWeaponsDrone)
			{
				ItemQualityCounts DroneWeapons = ItemQualitiesContent.ItemQualityGroups.DroneWeapons.GetItemCounts(self.inventory);
				int total = (DroneWeapons.BaseItemCount > 0 ? 1 : 0) +
							DroneWeapons.UncommonCount * 2 +
							DroneWeapons.RareCount * 3 +
							DroneWeapons.EpicCount * 4 +
							DroneWeapons.LegendaryCount * 5;
				return total;
			}
			return result;
		}

		static void DroneWeaponsBehavior_FixedUpdate(On.RoR2.DroneWeaponsBehavior.orig_FixedUpdate orig, DroneWeaponsBehavior self) {
			orig(self);
			if (self.body.master.IsDeployableLimited(DeployableSlot.DroneWeaponsDrone))
			{
				switch(ItemQualitiesContent.ItemQualityGroups.DroneWeapons.GetHighestQualityInInventory(self.body.master.inventory)) {
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
