using RoR2;
using UnityEngine;

namespace ItemQualities.Items
{
	static class IgniteOnKill
	{
		[SystemInitializer]
		static void Init()
		{
			On.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
		}

		private static void CharacterBody_RecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, RoR2.CharacterBody self)
		{
			orig(self);
			Transform fireAura = self.gameObject.transform.Find("FireAura(Clone)");
			if (!fireAura) return;
			IcicleAuraController auraController = fireAura.GetComponent<IcicleAuraController>();
			if(!auraController) return;

			ItemQualityCounts IgniteOnKill = ItemQualitiesContent.ItemQualityGroups.IgniteOnKill.GetItemCounts(self.master.inventory);
			auraController.icicleDamageCoefficientPerTick = IgniteOnKill.UncommonCount * 1 +
														IgniteOnKill.RareCount * 2 +
														IgniteOnKill.EpicCount * 3 +
														IgniteOnKill.LegendaryCount * 5;

			switch (ItemQualitiesContent.ItemQualityGroups.IgniteOnKill.GetHighestQualityInInventory(self.master.inventory))
			{
				case QualityTier.Uncommon:
					auraController.baseIcicleMax = 4;
					auraController.icicleDuration = 3;
					break;
				case QualityTier.Rare:
					auraController.baseIcicleMax = 8;
					auraController.icicleDuration = 5;
					break;
				case QualityTier.Epic:
					auraController.baseIcicleMax = 12;
					auraController.icicleDuration = 7;
					break;
				case QualityTier.Legendary:
					auraController.baseIcicleMax = 20;
					auraController.icicleDuration = 10;
					break;
			}
		}
	}
}

