using R2API;
using RoR2;
using System;
using UnityEngine;

namespace ItemQualities.Items
{
    static class PersonalShield
    {
        [SystemInitializer]
        static void Init()
        {
			RecalculateStatsAPI.GetStatCoefficients += getStatCoefficients;
			GlobalEventManager.OnInteractionsGlobal += OnInteractionsGlobal;
			GlobalEventManager.onServerDamageDealt += onServerDamageDealt;
		}

		private static void onServerDamageDealt(DamageReport report)
		{
			CharacterBody victim = report.victimBody;
			if (!victim) return;
			victim.SetBuffCount(ItemQualitiesContent.Buffs.PersonalShield.buffIndex, (int)(victim.GetBuffCount(ItemQualitiesContent.Buffs.PersonalShield) - report.damageDealt));
		}

		private static void OnInteractionsGlobal(Interactor interactor, IInteractable interactable, GameObject @object)
		{
			CharacterBody body = interactor.GetComponent<CharacterBody>();
			if (!body) return;
			float shieldPerInteract = 0;
			switch(ItemQualitiesContent.ItemQualityGroups.PersonalShield.GetHighestQualityInInventory(body.master.inventory)) {
				case QualityTier.Uncommon:
					shieldPerInteract = 0.01f;
					break;
				case QualityTier.Rare:
					shieldPerInteract = 0.02f;
					break;
				case QualityTier.Epic:
					shieldPerInteract = 0.03f;
					break;
				case QualityTier.Legendary:
					shieldPerInteract = 0.04f;
					break;
			}
			ItemQualityCounts Qualities = ItemQualitiesContent.ItemQualityGroups.PersonalShield.GetItemCounts(body.master.inventory);
			float MaxShield = Qualities.UncommonCount * 0.2f +
							Qualities.RareCount * 0.5f +
							Qualities.EpicCount * 0.9f +
							Qualities.LegendaryCount * 2f;

			body.SetBuffCount(ItemQualitiesContent.Buffs.PersonalShield.buffIndex, (int)Math.Min(body.GetBuffCount(ItemQualitiesContent.Buffs.PersonalShield) + shieldPerInteract * body.maxHealth, MaxShield * body.maxHealth));
		}

		private static void getStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
		{
			if (!sender) return;
			args.baseShieldAdd += sender.GetBuffCount(ItemQualitiesContent.Buffs.PersonalShield);
		}
	}
}
